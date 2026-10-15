using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GoRide.Notification.Models.Entities;

/// <summary>
/// Represents a rider's notification channel preferences.
/// Defaults to push-only when no explicit record exists in the database.
/// </summary>
[Table("notification_preferences")]
public class NotificationPreference
{
    /// <summary>
    /// Unique identifier of the rider (primary key).
    /// </summary>
    [Key]
    [Column("rider_id")]
    [StringLength(100)]
    public string RiderId { get; set; } = default!;

    /// <summary>
    /// Indicates whether push notifications are enabled for this rider. Defaults to true.
    /// </summary>
    [Column("push_enabled")]
    public bool PushEnabled { get; set; } = true;

    /// <summary>
    /// Indicates whether email notifications are enabled for this rider. Defaults to false.
    /// </summary>
    [Column("email_enabled")]
    public bool EmailEnabled { get; set; } = false;

    /// <summary>
    /// Timestamp when preferences were last updated.
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a registered FCM device token for a rider's mobile or web client.
/// </summary>
[Table("device_tokens")]
public class DeviceToken
{
    /// <summary>
    /// Auto-incremented primary key.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// Unique identifier of the rider owning this device token.
    /// </summary>
    [Required]
    [Column("rider_id")]
    [StringLength(100)]
    public string RiderId { get; set; } = default!;

    /// <summary>
    /// Firebase Cloud Messaging registration token string.
    /// </summary>
    [Required]
    [Column("token")]
    [StringLength(500)]
    public string Token { get; set; } = default!;

    /// <summary>
    /// Timestamp when this token was registered.
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when this token was last refreshed or updated.
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Audit log recording every notification dispatch attempt and delivery outcome.
/// </summary>
[Table("notification_logs")]
public class NotificationLog
{
    /// <summary>
    /// Auto-incremented log entry ID.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// Associated domain event ID (e.g. Kafka message EventId).
    /// </summary>
    [Required]
    [Column("event_id")]
    [StringLength(100)]
    public string EventId { get; set; } = default!;

    /// <summary>
    /// Target rider receiving the notification.
    /// </summary>
    [Required]
    [Column("rider_id")]
    [StringLength(100)]
    public string RiderId { get; set; } = default!;

    /// <summary>
    /// Communication channel used ("push", "email", etc.).
    /// </summary>
    [Required]
    [Column("channel")]
    [StringLength(50)]
    public string Channel { get; set; } = default!;

    /// <summary>
    /// Delivery outcome status ("sent", "failed", "skipped").
    /// </summary>
    [Required]
    [Column("status")]
    [StringLength(50)]
    public string Status { get; set; } = default!;

    /// <summary>
    /// Measured latency in milliseconds for sending the notification.
    /// </summary>
    [Column("latency_ms")]
    public int LatencyMs { get; set; }

    /// <summary>
    /// Exception message or error details if delivery failed.
    /// </summary>
    [Column("error")]
    public string? Error { get; set; }

    /// <summary>
    /// Timestamp when the log entry was created.
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Stores processed domain event IDs to guarantee idempotent event consumption.
/// </summary>
[Table("processed_events")]
public class ProcessedEvent
{
    /// <summary>
    /// Unique event identifier (primary key).
    /// </summary>
    [Key]
    [Column("event_id")]
    [StringLength(100)]
    public string EventId { get; set; } = default!;

    /// <summary>
    /// Timestamp when the event was processed.
    /// </summary>
    [Column("processed_at")]
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
