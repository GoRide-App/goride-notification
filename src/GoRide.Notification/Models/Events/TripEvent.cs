using System.Text.Json.Serialization;

namespace GoRide.Notification.Models.Events;

/// <summary>
/// Data model representing a trip domain event published to the Kafka trip-events topic.
/// </summary>
public class TripEvent
{
    /// <summary>
    /// Unique event GUID for idempotency tracking.
    /// </summary>
    [JsonPropertyName("eventId")]
    public string EventId { get; set; } = default!;

    /// <summary>
    /// Event type string (e.g. "DRIVER_ACCEPTED", "TRIP_CANCELLED").
    /// </summary>
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = default!;

    /// <summary>
    /// Unique trip identifier.
    /// </summary>
    [JsonPropertyName("tripId")]
    public string TripId { get; set; } = default!;

    /// <summary>
    /// Unique rider identifier receiving notification.
    /// </summary>
    [JsonPropertyName("riderId")]
    public string RiderId { get; set; } = default!;

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    [JsonPropertyName("occurredAt")]
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional event-specific details (driver details, vehicle type, ETA).
    /// </summary>
    [JsonPropertyName("payload")]
    public TripEventPayload Payload { get; set; } = new();
}

/// <summary>
/// Payload container containing driver details, vehicle information, and estimated arrival time.
/// </summary>
public class TripEventPayload
{
    /// <summary>
    /// Full name of the driver who accepted the trip.
    /// </summary>
    [JsonPropertyName("driverName")]
    public string? DriverName { get; set; }

    /// <summary>
    /// License plate number of the assigned vehicle.
    /// </summary>
    [JsonPropertyName("vehiclePlate")]
    public string? VehiclePlate { get; set; }

    /// <summary>
    /// Vehicle type (e.g. "TUKTUK", "CAR", "BIKE").
    /// </summary>
    [JsonPropertyName("vehicleType")]
    public string? VehicleType { get; set; }

    /// <summary>
    /// Estimated arrival time in minutes.
    /// </summary>
    [JsonPropertyName("etaMinutes")]
    public int? EtaMinutes { get; set; }

    /// <summary>
    /// Final trip fare amount.
    /// </summary>
    [JsonPropertyName("fare")]
    public decimal? Fare { get; set; }
}