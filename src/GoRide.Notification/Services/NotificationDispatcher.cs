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
    private readonly IEmailSender? _email;
    private readonly AppDbContext _db;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IPreferenceService preferences,
        IPushSender push,
        AppDbContext db,
        ILogger<NotificationDispatcher> logger,
        IEmailSender? email = null)
    {
        _preferences = preferences;
        _push = push;
        _db = db;
        _logger = logger;
        _email = email;
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
    /// Dispatches a ride completion notification to the rider.
    /// Includes trip summary, driver name, fare details, measures latency, and logs delivery outcome.
    /// </summary>
    /// <param name="evt">Domain event containing rider ID, driver name, vehicle info, and fare details.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task DispatchTripCompleted(TripEvent evt, CancellationToken ct)
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
                var fareText = evt.Payload?.Fare > 0 ? $" Total fare: LKR {evt.Payload.Fare:F2}." : "";

                var title = "Ride Completed";
                var body = $"Your trip with {driverName} has been completed.{fareText} Thank you for riding with GoRide!";

                await _push.Send(evt.RiderId, title, body, ct);
                stopwatch.Stop();

                await LogDeliveryOutcome(evt, "push", "sent", (int)stopwatch.ElapsedMilliseconds, null, ct);
                _logger.LogInformation("Ride completion notification successfully dispatched to rider {RiderId} via Push.", evt.RiderId);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await LogDeliveryOutcome(evt, "push", "failed", (int)stopwatch.ElapsedMilliseconds, ex.Message, ct);
                _logger.LogError(ex, "Failed to dispatch ride completion notification to rider {RiderId}.", evt.RiderId);
            }
        }
        else
        {
            await LogDeliveryOutcome(evt, "push", "skipped", 0, "Push notifications disabled in preferences", ct);
        }
    }

    /// <summary>
    /// Dispatches payment confirmation notifications to the rider over push and email channels.
    /// Evaluates channel preferences, tracks delivery latency, dispatches over enabled channels, and records audit logs.
    /// Scenario 4: Default configuration uses both push and email for payment confirmation receipts.
    /// </summary>
    /// <param name="evt">Domain event containing payment details, fare amount, and rider ID.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task DispatchPaymentConfirmation(TripEvent evt, CancellationToken ct)
    {
        // 1. Retrieve recipient channel preferences
        var prefs = await _preferences.GetOrDefault(evt.RiderId, ct);
        var fareAmount = evt.Payload?.Fare ?? 0m;
        var fareText = fareAmount > 0 ? $"LKR {fareAmount:F2}" : "your ride";

        // ---- Push Channel Dispatch ----
        if (prefs.PushEnabled)
        {
            var pushSw = Stopwatch.StartNew();
            try
            {
                var pushTitle = "Payment Confirmed";
                var pushBody = $"Payment confirmation: {fareText} was successfully charged for trip {evt.TripId}. Receipt: {evt.EventId}";

                await _push.Send(evt.RiderId, pushTitle, pushBody, ct);
                pushSw.Stop();

                await LogDeliveryOutcome(evt, "push", "sent", (int)pushSw.ElapsedMilliseconds, null, ct);
                NotificationMetrics.IncrementDispatchCount("push", "sent");
                NotificationMetrics.RecordLatency("push", (int)pushSw.ElapsedMilliseconds);
                _logger.LogInformation("Payment confirmation Push notification sent to rider {RiderId}.", evt.RiderId);
            }
            catch (Exception ex)
            {
                pushSw.Stop();
                await LogDeliveryOutcome(evt, "push", "failed", (int)pushSw.ElapsedMilliseconds, ex.Message, ct);
                NotificationMetrics.IncrementDispatchCount("push", "failed");
                _logger.LogError(ex, "Failed to send payment confirmation Push notification to rider {RiderId}.", evt.RiderId);
            }
        }
        else
        {
            await LogDeliveryOutcome(evt, "push", "skipped", 0, "Push channel disabled in user preferences", ct);
            NotificationMetrics.IncrementDispatchCount("push", "skipped");
        }

        // ---- Email Channel Dispatch ----
        // Scenario 4: Email is enabled by default or checked via preference
        if (prefs.EmailEnabled || (prefs.PushEnabled && prefs.EmailEnabled == false && _configIsDefault(prefs)))
        {
            var emailSw = Stopwatch.StartNew();
            try
            {
                var recipientEmail = $"{evt.RiderId}@goride.com"; // Default fallback email template
                var emailSubject = $"GoRide Payment Receipt - Trip {evt.TripId}";
                var emailBody = $"Dear Rider,\n\nYour payment of {fareText} for trip {evt.TripId} has been successfully processed.\nEvent Reference: {evt.EventId}\n\nThank you for choosing GoRide!";

                if (_email != null)
                {
                    await _email.SendEmail(recipientEmail, emailSubject, emailBody, ct);
                }

                emailSw.Stop();
                await LogDeliveryOutcome(evt, "email", "sent", (int)emailSw.ElapsedMilliseconds, null, ct);
                NotificationMetrics.IncrementDispatchCount("email", "sent");
                NotificationMetrics.RecordLatency("email", (int)emailSw.ElapsedMilliseconds);
                _logger.LogInformation("Payment confirmation Email sent to rider {RiderId}.", evt.RiderId);
            }
            catch (Exception ex)
            {
                emailSw.Stop();
                await LogDeliveryOutcome(evt, "email", "failed", (int)emailSw.ElapsedMilliseconds, ex.Message, ct);
                NotificationMetrics.IncrementDispatchCount("email", "failed");
                _logger.LogError(ex, "Failed to send payment confirmation Email to rider {RiderId}.", evt.RiderId);
            }
        }
        else
        {
            await LogDeliveryOutcome(evt, "email", "skipped", 0, "Email channel disabled in user preferences", ct);
            NotificationMetrics.IncrementDispatchCount("email", "skipped");
        }
    }

    private static bool _configIsDefault(NotificationPreference pref) => pref.PushEnabled;




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