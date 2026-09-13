using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using GoRide.Notification.Data;
using GoRide.Notification.Models.Entities;
using GoRide.Notification.Models.Events;
using GoRide.Notification.Services;

namespace GoRide.Notification.Events.Consumers;

/// <summary>
/// Background hosted service that consumes trip lifecycle domain events from Kafka / Redpanda topics.
/// Filters for DRIVER_ACCEPTED events, guarantees idempotent processing using ProcessedEvents, and commits Kafka offsets manually.
/// </summary>
public class TripEventConsumerService : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TripEventConsumerService> _logger;
    private readonly string _topic;

    public TripEventConsumerService(
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        ILogger<TripEventConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _topic = config["Kafka:TripEventsTopic"] ?? "goride.trip.events";

        var kafkaConfig = new ConsumerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = config["Kafka:GroupId"] ?? "goride-notification-group",
            SecurityProtocol = Enum.Parse<SecurityProtocol>(config["Kafka:SecurityProtocol"] ?? "Plaintext", ignoreCase: true),
            SaslMechanism = !string.IsNullOrEmpty(config["Kafka:SaslMechanism"])
                ? Enum.Parse<SaslMechanism>(config["Kafka:SaslMechanism"]!, ignoreCase: true)
                : null,
            SaslUsername = config["Kafka:SaslUsername"],
            SaslPassword = config["Kafka:SaslPassword"],
            EnableAutoCommit = false, // Manual offset commit after successful dispatch & DB transaction
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        _consumer = new ConsumerBuilder<string, string>(kafkaConfig).Build();
    }

    /// <summary>
    /// Executes the background Kafka consumer polling loop until application cancellation is requested.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token triggered on service shutdown.</param>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(_topic);
        _logger.LogInformation("TripEventConsumerService subscribed to Kafka topic: {Topic}", _topic);

        return Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;
                try
                {
                    result = _consumer.Consume(stoppingToken);
                    if (result == null || string.IsNullOrWhiteSpace(result.Message?.Value))
                    {
                        continue;
                    }

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var evt = JsonSerializer.Deserialize<TripEvent>(result.Message.Value, options);

                    if (evt == null || string.IsNullOrWhiteSpace(evt.EventType))
                    {
                        _consumer.Commit(result);
                        continue;
                    }

                    bool isDriverAccepted = string.Equals(evt.EventType, "DRIVER_ACCEPTED", StringComparison.OrdinalIgnoreCase);
                    bool isDriverArrived = string.Equals(evt.EventType, "DRIVER_ARRIVED", StringComparison.OrdinalIgnoreCase);
                    bool isTripCompleted = string.Equals(evt.EventType, "TRIP_COMPLETED", StringComparison.OrdinalIgnoreCase);
                    bool isPaymentConfirmed = string.Equals(evt.EventType, "PAYMENT_CONFIRMED", StringComparison.OrdinalIgnoreCase) || string.Equals(evt.EventType, "PAYMENT_SUCCESS", StringComparison.OrdinalIgnoreCase);

                    // Only handle supported trip notification events
                    if (!isDriverAccepted && !isDriverArrived && !isTripCompleted && !isPaymentConfirmed)
                    {
                        _consumer.Commit(result);
                        continue;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // Idempotency check: verify if this EventId has already been processed
                    bool alreadyProcessed = await db.ProcessedEvents.AnyAsync(p => p.EventId == evt.EventId, stoppingToken);
                    if (alreadyProcessed)
                    {
                        _logger.LogInformation("Event {EventId} was already processed. Skipping duplicate.", evt.EventId);
                        _consumer.Commit(result);
                        continue;
                    }

                    // Dispatch notification to rider based on event type
                    var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
                    if (isDriverAccepted)
                    {
                        await dispatcher.DispatchDriverAccepted(evt, stoppingToken);
                    }
                    else if (isDriverArrived)
                    {
                        await dispatcher.DispatchDriverArrived(evt, stoppingToken);
                    }
                    else if (isTripCompleted)
                    {
                        await dispatcher.DispatchTripCompleted(evt, stoppingToken);
                    }
                    else if (isPaymentConfirmed)
                    {
                        await dispatcher.DispatchPaymentConfirmation(evt, stoppingToken);
                    }

                    // Mark event as processed
                    db.ProcessedEvents.Add(new ProcessedEvent
                    {
                        EventId = evt.EventId,
                        ProcessedAt = DateTime.UtcNow
                    });

                    await db.SaveChangesAsync(stoppingToken);

                    // Commit offset only after successful processing and DB save
                    _consumer.Commit(result);
                }
                catch (ConsumeException ex)
                {
                    if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                    {
                        _logger.LogWarning("Kafka topic '{Topic}' does not exist yet on broker. Retrying in 2 seconds...", _topic);
                        await Task.Delay(2000, stoppingToken);
                    }
                    else
                    {
                        _logger.LogError(ex, "Kafka consumption error occurred while reading from topic {Topic}.", _topic);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("TripEventConsumerService polling canceled.");
                    break;
                }
                catch (Exception ex) when (result is not null)
                {
                    _logger.LogError(ex, "Failed processing trip event. Kafka offset will not be committed so message will be redelivered.");
                }
            }

            _consumer.Close();
        }, stoppingToken);
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}