using System.Diagnostics;
using GoRide.Notification.Data;
using GoRide.Notification.Models.Entities;
using GoRide.Notification.Models.Events;

namespace GoRide.Notification.Services;

/// <summary>
/// Core notification dispatcher implementation.
/// Evaluates recipient channel preferences, triggers push notifications, and records delivery metrics and status in NotificationLogs.
/// </summary>
public class NotificationDispatcher : INotificationDispatcher
{
    private readonly IPreferenceService _preferences;
    private readonly IPushSender _push;
    private readonly AppDbContext _db;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IPreferenceService preferences,
        IPushSender push,
        AppDbContext db,
        ILogger<NotificationDispatcher> logger)
    {
        _preferences = preferences;
        _push = push;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Dispatches a driver acceptance notification to the rider.
    /// Checks channel preferences, measures dispatch latency, sends push message, and records delivery audit log.
    /// </summary>
    /// <param name="evt">Domain event containing rider ID, driver name, vehicle information, and ETA.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task DispatchDriverAccepted(TripEvent evt, CancellationToken ct)
    {
        // 1. Retrieve recipient preferences (defaults to push=true, email=false)
        var prefs = await _preferences.GetOrDefault(evt.RiderId, ct);

        // 2. Process push notification if enabled
        if (prefs.PushEnabled)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var driverName = string.IsNullOrWhiteSpace(evt.Payload?.DriverName) ? "Your driver" : evt.Payload.DriverName;
                var eta = evt.Payload?.EtaMinutes ?? 5;

                var title = "Driver on the way";
                var body = $"{driverName} accepted your ride request. ETA: {eta} min.";

                await _push.Send(evt.RiderId, title, body, ct);
                stopwatch.Stop();

                await LogDeliveryOutcome(evt, "push", "sent", (int)stopwatch.ElapsedMilliseconds, null, ct);
                _logger.LogInformation("Driver accepted notification successfully dispatched to rider {RiderId} via Push.", evt.RiderId);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await LogDeliveryOutcome(evt, "push", "failed", (int)stopwatch.ElapsedMilliseconds, ex.Message, ct);
                _logger.LogError(ex, "Failed to dispatch driver accepted notification to rider {RiderId}.", evt.RiderId);
            }
        }
        else
        {
            await LogDeliveryOutcome(evt, "push", "skipped", 0, "Push notifications disabled in preferences", ct);
        }
    }

    /// <summary>
    /// Dispatches a driver arrival notification to the rider.
    /// Checks channel preferences, measures dispatch latency, sends push message, and records delivery audit log.
    /// </summary>
    /// <param name="evt">Domain event containing rider ID, driver name, and vehicle info on arrival.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task DispatchDriverArrived(TripEvent evt, CancellationToken ct)
    {
        // 1. Retrieve recipient preferences (defaults to push=true, email=false)
        var prefs = await _preferences.GetOrDefault(evt.RiderId, ct);

        // 2. Process push notification if enabled
        if (prefs.PushEnabled)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var driverName = string.IsNullOrWhiteSpace(evt.Payload?.DriverName) ? "Your driver" : evt.Payload.DriverName;

                var title = "Driver Arrived";
                var body = $"{driverName} has arrived at your pickup location.";

                await _push.Send(evt.RiderId, title, body, ct);
                stopwatch.Stop();

                await LogDeliveryOutcome(evt, "push", "sent", (int)stopwatch.ElapsedMilliseconds, null, ct);
                _logger.LogInformation("Driver arrived notification successfully dispatched to rider {RiderId} via Push.", evt.RiderId);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await LogDeliveryOutcome(evt, "push", "failed", (int)stopwatch.ElapsedMilliseconds, ex.Message, ct);
                _logger.LogError(ex, "Failed to dispatch driver arrived notification to rider {RiderId}.", evt.RiderId);
            }
        }
        else
        {
            await LogDeliveryOutcome(evt, "push", "skipped", 0, "Push notifications disabled in preferences", ct);
        }
    }


    /// <summary>
    /// Helper method to record notification delivery outcomes in the NotificationLogs database table.
    /// </summary>
    private async Task LogDeliveryOutcome(
        TripEvent evt,
        string channel,
        string status,
        int latencyMs,
        string? error,
        CancellationToken ct)
    {
        _db.NotificationLogs.Add(new NotificationLog
        {
            EventId = evt.EventId,
            RiderId = evt.RiderId,
            Channel = channel,
            Status = status,
            LatencyMs = latencyMs,
            Error = error,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }
}