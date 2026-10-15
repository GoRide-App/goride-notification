using Microsoft.EntityFrameworkCore;
using GoRide.Notification.Data;
using GoRide.Notification.Models.Entities;

namespace GoRide.Notification.Services;

/// <summary>
/// Manages retrieval of rider notification channel preferences.
/// Falls back to push-only notifications when no explicit database record exists.
/// </summary>
public class PreferenceService : IPreferenceService
{
    private readonly AppDbContext _db;

    public PreferenceService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Looks up rider notification channel preferences from the database.
    /// Returns default preferences (PushEnabled = true, EmailEnabled = false) if the rider has not saved custom settings.
    /// </summary>
    /// <param name="riderId">Unique ID of the rider.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Resolved NotificationPreference object.</returns>
    public async Task<NotificationPreference> GetOrDefault(string riderId, CancellationToken ct)
    {
        var pref = await _db.NotificationPreferences.FirstOrDefaultAsync(p => p.RiderId == riderId, ct);

        return pref ?? new NotificationPreference
        {
            RiderId = riderId,
            PushEnabled = true,
            EmailEnabled = false,
            UpdatedAt = DateTime.UtcNow
        };
    }
}