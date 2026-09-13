using GoRide.Notification.Models.Entities;
using GoRide.Notification.Models.Events;

namespace GoRide.Notification.Services;

/// <summary>
/// Handles notification dispatch logic for domain events, evaluating preferences, channel senders, and delivery logging.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Dispatches a notification to the rider when a driver accepts their ride request.
    /// </summary>
    /// <param name="evt">The DRIVER_ACCEPTED domain event payload.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DispatchDriverAccepted(TripEvent evt, CancellationToken ct);

    /// <summary>
    /// Dispatches a notification to the rider when their driver arrives at the pickup location.
    /// </summary>
    /// <param name="evt">The DRIVER_ARRIVED domain event payload.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DispatchDriverArrived(TripEvent evt, CancellationToken ct);

    /// <summary>
    /// Dispatches a notification to the rider when their ride is completed.
    /// </summary>
    /// <param name="evt">The TRIP_COMPLETED domain event payload.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DispatchTripCompleted(TripEvent evt, CancellationToken ct);

    /// <summary>
    /// Dispatches payment confirmation notifications over enabled channels (Push & Email) with latency tracking and delivery logging.
    /// </summary>
    /// <param name="evt">The PAYMENT_CONFIRMED domain event payload.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DispatchPaymentConfirmation(TripEvent evt, CancellationToken ct);
}

/// <summary>
/// Retrieves rider notification preferences or defaults to push and email enabled for critical payment alerts.
/// </summary>
public interface IPreferenceService
{
    /// <summary>
    /// Retrieves notification preferences for a rider, returning default preferences if none exist.
    /// </summary>
    /// <param name="riderId">Unique ID of the rider.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Rider's notification preference.</returns>
    Task<NotificationPreference> GetOrDefault(string riderId, CancellationToken ct);
}

/// <summary>
/// Contract for sending push notifications to rider device tokens.
/// </summary>
public interface IPushSender
{
    /// <summary>
    /// Sends a push notification message to all device tokens registered for a rider.
    /// </summary>
    /// <param name="riderId">Target rider ID.</param>
    /// <param name="title">Notification headline title.</param>
    /// <param name="body">Notification message body.</param>
    /// <param name="ct">Cancellation token.</param>
    Task Send(string riderId, string title, string body, CancellationToken ct);
}

/// <summary>
/// Contract for sending email notifications to riders.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends an email notification to the specified recipient.
    /// </summary>
    /// <param name="recipientEmail">Target recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="body">Email message body content.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendEmail(string recipientEmail, string subject, string body, CancellationToken ct);
}
