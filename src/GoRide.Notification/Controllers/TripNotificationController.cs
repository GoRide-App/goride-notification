using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using GoRide.Notification.Models.Events;
using GoRide.Notification.Services;

namespace GoRide.Notification.Controllers;

/// <summary>
/// API controller for processing trip notification triggers and validating state machine transitions.
/// </summary>
[ApiController]
[Route("api/notifications/trip")]
public class TripNotificationController : ControllerBase
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<TripNotificationController> _logger;

    public TripNotificationController(
        INotificationDispatcher dispatcher,
        ILogger<TripNotificationController> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <summary>
    /// Triggers driver arrival notification after validating the trip state transition.
    /// Valid states for arrival trigger: DRIVER_ASSIGNED, DRIVER_EN_ROUTE, ACCEPTED.
    /// </summary>
    /// <param name="request">Trip notification payload including current state.</param>
    /// <returns>HTTP 200 OK on valid transition, HTTP 409 Conflict if state is invalid.</returns>
    [HttpPost("driver-arrived")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerDriverArrived([FromBody] DriverArrivalNotificationRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Valid trip lifecycle states for triggering arrival notification
        var validStates = new[] { "DRIVER_ASSIGNED", "DRIVER_EN_ROUTE", "ACCEPTED" };
        bool isValidState = Array.Exists(validStates, s => string.Equals(s, request.CurrentState, StringComparison.OrdinalIgnoreCase));

        if (!isValidState)
        {
            _logger.LogWarning(
                "Invalid transition rejected for trip {TripId}: current state '{State}' is invalid for driver arrival trigger.",
                request.TripId, request.CurrentState);

            return Conflict(new
            {
                status = "Conflict",
                message = $"Trip '{request.TripId}' is not in a valid state for driver arrival notification. Current state: '{request.CurrentState}'.",
                tripId = request.TripId,
                currentState = request.CurrentState
            });
        }

        var evt = new TripEvent
        {
            EventId = string.IsNullOrWhiteSpace(request.EventId) ? $"evt-arrived-{Guid.NewGuid():N}" : request.EventId,
            EventType = "DRIVER_ARRIVED",
            TripId = request.TripId,
            RiderId = request.RiderId,
            OccurredAt = DateTime.UtcNow,
            Payload = new TripEventPayload
            {
                DriverName = request.DriverName ?? "Your driver",
                VehicleType = request.VehicleType ?? "GoRide",
                VehiclePlate = request.VehiclePlate ?? ""
            }
        };

        await _dispatcher.DispatchDriverArrived(evt, ct);

        return Ok(new
        {
            status = "DRIVER_ARRIVED",
            message = "Driver arrival notification dispatched successfully",
            tripId = request.TripId,
            nextState = "DRIVER_ARRIVED"
        });
    }

    /// <summary>
    /// Triggers ride completion notification after validating the trip state machine transition.
    /// Valid states for completion trigger: TRIP_IN_PROGRESS, DRIVER_ARRIVED, ARRIVED, PAYMENT_PENDING, PAID.
    /// </summary>
    /// <param name="request">Trip completed notification payload including current state.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>HTTP 200 OK on valid transition, HTTP 409 Conflict if state is invalid.</returns>
    [HttpPost("completed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerTripCompleted([FromBody] TripCompletedNotificationRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Valid trip lifecycle states for triggering completion notification
        var validStates = new[] { "TRIP_IN_PROGRESS", "DRIVER_ARRIVED", "ARRIVED", "PAYMENT_PENDING", "PAID" };
        bool isValidState = Array.Exists(validStates, s => string.Equals(s, request.CurrentState, StringComparison.OrdinalIgnoreCase));

        if (!isValidState)
        {
            _logger.LogWarning(
                "Invalid transition rejected for trip {TripId}: current state '{State}' is invalid for trip completed trigger.",
                request.TripId, request.CurrentState);

            return Conflict(new
            {
                status = "Conflict",
                message = $"Trip '{request.TripId}' is not in a valid state for trip completion notification. Current state: '{request.CurrentState}'.",
                tripId = request.TripId,
                currentState = request.CurrentState
            });
        }

        var evt = new TripEvent
        {
            EventId = string.IsNullOrWhiteSpace(request.EventId) ? $"evt-completed-{Guid.NewGuid():N}" : request.EventId,
            EventType = "TRIP_COMPLETED",
            TripId = request.TripId,
            RiderId = request.RiderId,
            OccurredAt = DateTime.UtcNow,
            Payload = new TripEventPayload
            {
                DriverName = request.DriverName ?? "Your driver",
                VehicleType = request.VehicleType ?? "GoRide",
                VehiclePlate = request.VehiclePlate ?? "",
                Fare = request.Fare
            }
        };

        await _dispatcher.DispatchTripCompleted(evt, ct);

        return Ok(new
        {
            status = "TRIP_COMPLETED",
            message = "Ride completion notification dispatched successfully",
            tripId = request.TripId,
            nextState = "TRIP_COMPLETED"
        });
    }

    /// <summary>
    /// Triggers payment confirmation notification to rider over push and email channels.
    /// Scenario 1: Sent to enabled channels within target latency upon PAYMENT_CONFIRMED event.
    /// Scenario 4: Default configuration uses both push and email channels for payment receipts.
    /// </summary>
    /// <param name="request">Payment notification request payload.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("payment-confirmed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerPaymentConfirmed([FromBody] PaymentConfirmedNotificationRequest request, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var evt = new TripEvent
        {
            EventId = string.IsNullOrWhiteSpace(request.EventId) ? $"evt-pay-{Guid.NewGuid():N}" : request.EventId,
            EventType = "PAYMENT_CONFIRMED",
            TripId = request.TripId,
            RiderId = request.RiderId,
            OccurredAt = DateTime.UtcNow,
            Payload = new TripEventPayload
            {
                Fare = request.Fare
            }
        };

        await _dispatcher.DispatchPaymentConfirmation(evt, ct);

        return Ok(new
        {
            status = "PAYMENT_CONFIRMED",
            message = "Payment confirmation notifications processed for push and email channels",
            tripId = request.TripId,
            channels = new[] { "push", "email" }
        });
    }
}

/// <summary>
/// Request DTO for triggering payment confirmation notification.
/// </summary>
public class PaymentConfirmedNotificationRequest
{
    /// <summary>
    /// Event identifier (optional, auto-generated if omitted).
    /// </summary>
    public string? EventId { get; set; }

    /// <summary>
    /// Unique trip identifier.
    /// </summary>
    [Required]
    public string TripId { get; set; } = default!;

    /// <summary>
    /// Unique rider identifier.
    /// </summary>
    [Required]
    public string RiderId { get; set; } = default!;

    /// <summary>
    /// Total fare amount confirmed for payment.
    /// </summary>
    public decimal Fare { get; set; }
}

/// <summary>
/// Request DTO for triggering ride completion notification.
/// </summary>
public class TripCompletedNotificationRequest
{
    /// <summary>
    /// Event identifier (optional, auto-generated if omitted).
    /// </summary>
    public string? EventId { get; set; }

    /// <summary>
    /// Unique trip identifier.
    /// </summary>
    [Required]
    public string TripId { get; set; } = default!;

    /// <summary>
    /// Unique rider identifier receiving the notification.
    /// </summary>
    [Required]
    public string RiderId { get; set; } = default!;

    /// <summary>
    /// Driver's display name.
    /// </summary>
    public string? DriverName { get; set; }

    /// <summary>
    /// Vehicle type string.
    /// </summary>
    public string? VehicleType { get; set; }

    /// <summary>
    /// Vehicle license plate number.
    /// </summary>
    public string? VehiclePlate { get; set; }

    /// <summary>
    /// Total fare amount for the completed trip.
    /// </summary>
    public decimal? Fare { get; set; }

    /// <summary>
    /// Current trip lifecycle state (e.g. TRIP_IN_PROGRESS, DRIVER_ARRIVED).
    /// </summary>
    [Required]
    public string CurrentState { get; set; } = default!;
}

/// <summary>
/// Request DTO for triggering driver arrival notification.
/// </summary>
public class DriverArrivalNotificationRequest
{
    /// <summary>
    /// Event identifier (optional, auto-generated if omitted).
    /// </summary>
    public string? EventId { get; set; }

    /// <summary>
    /// Unique trip identifier.
    /// </summary>
    [Required]
    public string TripId { get; set; } = default!;

    /// <summary>
    /// Unique rider identifier receiving the notification.
    /// </summary>
    [Required]
    public string RiderId { get; set; } = default!;

    /// <summary>
    /// Driver's display name.
    /// </summary>
    public string? DriverName { get; set; }

    /// <summary>
    /// Vehicle type string.
    /// </summary>
    public string? VehicleType { get; set; }

    /// <summary>
    /// Vehicle license plate number.
    /// </summary>
    public string? VehiclePlate { get; set; }

    /// <summary>
    /// Current trip lifecycle state (e.g. DRIVER_EN_ROUTE, DRIVER_ASSIGNED, ACCEPTED).
    /// </summary>
    [Required]
    public string CurrentState { get; set; } = default!;
}
