using Microsoft.EntityFrameworkCore;
using FirebaseAdmin.Messaging;
using GoRide.Notification.Data;

namespace GoRide.Notification.Services;

/// <summary>
/// Implementation of IPushSender using Firebase Cloud Messaging (FCM).
/// Sends push messages to registered rider device tokens with fallback logging for local development.
/// </summary>
public class FcmPushSender : IPushSender
{
    private readonly AppDbContext _db;
    private readonly FirebaseMessaging? _messaging;
    private readonly ILogger<FcmPushSender> _logger;

    public FcmPushSender(AppDbContext db, ILogger<FcmPushSender> logger, FirebaseMessaging? messaging = null)
    {
        _db = db;
        _logger = logger;
        _messaging = messaging;
    }

    /// <summary>
    /// Looks up all registered FCM tokens for the specified rider ID and dispatches push notifications.
    /// If no device tokens are registered, logs a warning and completes (or raises invalid operation if strict).
    /// </summary>
    /// <param name="riderId">Target rider ID.</param>
    /// <param name="title">Notification title.</param>
    /// <param name="body">Notification message body.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task Send(string riderId, string title, string body, CancellationToken ct)
    {
        var tokens = await _db.DeviceTokens
            .Where(t => t.RiderId == riderId)
            .Select(t => t.Token)
            .ToListAsync(ct);

        if (tokens.Count == 0)
        {
            _logger.LogWarning("No registered FCM device token found for rider {RiderId}. Simulating push dispatch.", riderId);
            return;
        }

        foreach (var token in tokens)
        {
            if (_messaging != null)
            {
                try
                {
                    await _messaging.SendAsync(new Message
                    {
                        Token = token,
                        Notification = new FirebaseAdmin.Messaging.Notification
                        {
                            Title = title,
                            Body = body
                        }
                    }, ct);

                    _logger.LogInformation("FCM Push notification sent to token {Token} for rider {RiderId}", token, riderId);
                }
                catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
                {
                    _logger.LogWarning("Token {Token} for rider {RiderId} is unregistered. Removing from database.", token, riderId);
                    
                    // Remove the stale token from the DB
                    var staleToken = await _db.DeviceTokens.FirstOrDefaultAsync(t => t.Token == token && t.RiderId == riderId, ct);
                    if (staleToken != null)
                    {
                        _db.DeviceTokens.Remove(staleToken);
                        await _db.SaveChangesAsync(ct);
                    }
                }
            }
            else
            {
                // Local development fallback log when Firebase credentials are not configured
                _logger.LogInformation("[DEV PUSH MOCK] Rider {RiderId} | Token: {Token} | Title: {Title} | Body: {Body}",
                    riderId, token, title, body);
            }
        }
    }
}