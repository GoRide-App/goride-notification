using Microsoft.EntityFrameworkCore;
using GoRide.Notification.Models.Entities;

namespace GoRide.Notification.Data;

/// <summary>
/// Entity Framework Core database context for the GoRide Notification Service.
/// Manages notification preferences, registered FCM device tokens, dispatch logs, and idempotent processed events.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Gets or sets rider notification channel preferences.
    /// </summary>
    public DbSet<NotificationPreference> NotificationPreferences { get; set; } = default!;

    /// <summary>
    /// Gets or sets registered FCM device tokens for riders.
    /// </summary>
    public DbSet<DeviceToken> DeviceTokens { get; set; } = default!;

    /// <summary>
    /// Gets or sets notification delivery log entries.
    /// </summary>
    public DbSet<NotificationLog> NotificationLogs { get; set; } = default!;

    /// <summary>
    /// Gets or sets idempotent processed Kafka event records.
    /// </summary>
    public DbSet<ProcessedEvent> ProcessedEvents { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure indexes for query optimization
        modelBuilder.Entity<DeviceToken>()
            .HasIndex(d => d.RiderId);

        modelBuilder.Entity<NotificationLog>()
            .HasIndex(n => n.EventId);

        modelBuilder.Entity<NotificationLog>()
            .HasIndex(n => n.RiderId);
    }
}
