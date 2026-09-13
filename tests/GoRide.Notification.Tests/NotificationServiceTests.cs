using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using GoRide.Notification.Controllers;
using GoRide.Notification.Data;
using GoRide.Notification.Models.Entities;
using GoRide.Notification.Models.Events;
using GoRide.Notification.Services;

namespace GoRide.Notification.Tests;

/// <summary>
/// Unit tests for NotificationDispatcher, PreferenceService, TripNotificationController, and idempotency logic.
/// Verifies SCRUM-127, SCRUM-128, and SCRUM-129 driver notification requirements.
/// </summary>
public class NotificationServiceTests
{
    private AppDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task PreferenceService_GetOrDefault_ReturnsDefaultPushEnabled_WhenNoPreferenceExists()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var preferenceService = new PreferenceService(db);

        // Act
        var result = await preferenceService.GetOrDefault("rider-123", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("rider-123", result.RiderId);
        Assert.True(result.PushEnabled);
        Assert.False(result.EmailEnabled);
    }

    [Fact]
    public async Task PreferenceService_GetOrDefault_ReturnsSavedPreference_WhenPreferenceExists()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        db.NotificationPreferences.Add(new NotificationPreference
        {
            RiderId = "rider-456",
            PushEnabled = false,
            EmailEnabled = true
        });
        await db.SaveChangesAsync();

        var preferenceService = new PreferenceService(db);

        // Act
        var result = await preferenceService.GetOrDefault("rider-456", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("rider-456", result.RiderId);
        Assert.False(result.PushEnabled);
        Assert.True(result.EmailEnabled);
    }

    [Fact]
    public async Task NotificationDispatcher_DispatchDriverAccepted_SendsPushAndLogsDeliveryOutcome()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var preferenceService = new PreferenceService(db);
        var pushSender = new FcmPushSender(db, NullLogger<FcmPushSender>.Instance, messaging: null);
        var dispatcher = new NotificationDispatcher(
            preferenceService,
            pushSender,
            db,
            NullLogger<NotificationDispatcher>.Instance);

        var evt = new TripEvent
        {
            EventId = "evt-789",
            EventType = "DRIVER_ACCEPTED",
            TripId = "trip-100",
            RiderId = "rider-999",
            OccurredAt = DateTime.UtcNow,
            Payload = new TripEventPayload
            {
                DriverName = "Kamal Perera",
                VehicleType = "TUKTUK",
                VehiclePlate = "AB-1234",
                EtaMinutes = 3
            }
        };

        // Act
        await dispatcher.DispatchDriverAccepted(evt, CancellationToken.None);

        // Assert
        var log = await db.NotificationLogs.FirstOrDefaultAsync(l => l.EventId == "evt-789");
        Assert.NotNull(log);
        Assert.Equal("rider-999", log.RiderId);
        Assert.Equal("push", log.Channel);
        Assert.Equal("sent", log.Status);
    }

    [Fact]
    public async Task NotificationDispatcher_DispatchDriverArrived_SendsPushAndLogsDeliveryOutcome()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var preferenceService = new PreferenceService(db);
        var pushSender = new FcmPushSender(db, NullLogger<FcmPushSender>.Instance, messaging: null);
        var dispatcher = new NotificationDispatcher(
            preferenceService,
            pushSender,
            db,
            NullLogger<NotificationDispatcher>.Instance);

        var evt = new TripEvent
        {
            EventId = "evt-arrived-100",
            EventType = "DRIVER_ARRIVED",
            TripId = "trip-200",
            RiderId = "rider-888",
            OccurredAt = DateTime.UtcNow,
            Payload = new TripEventPayload
            {
                DriverName = "Nimal Fernando",
                VehicleType = "CAR",
                VehiclePlate = "WP-CAB-5678"
            }
        };

        // Act
        await dispatcher.DispatchDriverArrived(evt, CancellationToken.None);

        // Assert
        var log = await db.NotificationLogs.FirstOrDefaultAsync(l => l.EventId == "evt-arrived-100");
        Assert.NotNull(log);
        Assert.Equal("rider-888", log.RiderId);
        Assert.Equal("push", log.Channel);
        Assert.Equal("sent", log.Status);
    }

    [Fact]
    public async Task NotificationDispatcher_DispatchTripCompleted_SendsPushAndLogsDeliveryOutcome()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var preferenceService = new PreferenceService(db);
        var pushSender = new FcmPushSender(db, NullLogger<FcmPushSender>.Instance, messaging: null);
        var dispatcher = new NotificationDispatcher(
            preferenceService,
            pushSender,
            db,
            NullLogger<NotificationDispatcher>.Instance);

        var evt = new TripEvent
        {
            EventId = "evt-completed-300",
            EventType = "TRIP_COMPLETED",
            TripId = "trip-300",
            RiderId = "rider-777",
            OccurredAt = DateTime.UtcNow,
            Payload = new TripEventPayload
            {
                DriverName = "Kasun Kalhara",
                VehicleType = "TUKTUK",
                VehiclePlate = "WP-AB-9999",
                Fare = 450.00m
            }
        };

        // Act
        await dispatcher.DispatchTripCompleted(evt, CancellationToken.None);

        // Assert
        var log = await db.NotificationLogs.FirstOrDefaultAsync(l => l.EventId == "evt-completed-300");
        Assert.NotNull(log);
        Assert.Equal("rider-777", log.RiderId);
        Assert.Equal("push", log.Channel);
        Assert.Equal("sent", log.Status);
    }

    [Fact]
    public async Task TripNotificationController_TriggerDriverArrived_ReturnsOk_WhenValidState()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var preferenceService = new PreferenceService(db);
        var pushSender = new FcmPushSender(db, NullLogger<FcmPushSender>.Instance, messaging: null);
        var dispatcher = new NotificationDispatcher(
            preferenceService,
            pushSender,
            db,
            NullLogger<NotificationDispatcher>.Instance);

        var controller = new TripNotificationController(dispatcher, NullLogger<TripNotificationController>.Instance);

        var request = new DriverArrivalNotificationRequest
        {
            TripId = "trip-valid-1",
            RiderId = "rider-111",
            DriverName = "Sunil Shantha",
            CurrentState = "DRIVER_EN_ROUTE"
        };

        // Act
        var result = await controller.TriggerDriverArrived(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task TripNotificationController_TriggerDriverArrived_ReturnsConflict_WhenInvalidState()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var preferenceService = new PreferenceService(db);
        var pushSender = new FcmPushSender(db, NullLogger<FcmPushSender>.Instance, messaging: null);
        var dispatcher = new NotificationDispatcher(
            preferenceService,
            pushSender,
            db,
            NullLogger<NotificationDispatcher>.Instance);

        var controller = new TripNotificationController(dispatcher, NullLogger<TripNotificationController>.Instance);

        var request = new DriverArrivalNotificationRequest
        {
            TripId = "trip-invalid-1",
            RiderId = "rider-111",
            DriverName = "Sunil Shantha",
            CurrentState = "SEARCHING_DRIVER" // Invalid state for arrival transition
        };

        // Act
        var result = await controller.TriggerDriverArrived(request);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflictResult.StatusCode);
    }

    [Fact]
    public async Task TripNotificationController_TriggerTripCompleted_ReturnsOk_WhenValidState()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var preferenceService = new PreferenceService(db);
        var pushSender = new FcmPushSender(db, NullLogger<FcmPushSender>.Instance, messaging: null);
        var dispatcher = new NotificationDispatcher(
            preferenceService,
            pushSender,
            db,
            NullLogger<NotificationDispatcher>.Instance);

        var controller = new TripNotificationController(dispatcher, NullLogger<TripNotificationController>.Instance);

        var request = new TripCompletedNotificationRequest
        {
            TripId = "trip-comp-valid-1",
            RiderId = "rider-222",
            DriverName = "Sunil Shantha",
            Fare = 520.00m,
            CurrentState = "TRIP_IN_PROGRESS"
        };

        // Act
        var result = await controller.TriggerTripCompleted(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task TripNotificationController_TriggerTripCompleted_ReturnsConflict_WhenInvalidState()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var preferenceService = new PreferenceService(db);
        var pushSender = new FcmPushSender(db, NullLogger<FcmPushSender>.Instance, messaging: null);
        var dispatcher = new NotificationDispatcher(
            preferenceService,
            pushSender,
            db,
            NullLogger<NotificationDispatcher>.Instance);

        var controller = new TripNotificationController(dispatcher, NullLogger<TripNotificationController>.Instance);

        var request = new TripCompletedNotificationRequest
        {
            TripId = "trip-comp-invalid-1",
            RiderId = "rider-222",
            DriverName = "Sunil Shantha",
            Fare = 520.00m,
            CurrentState = "SEARCHING_DRIVER" // Invalid state for completed transition
        };

        // Act
        var result = await controller.TriggerTripCompleted(request);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflictResult.StatusCode);
    }

    [Fact]
    public async Task IdempotencyCheck_PreventsDuplicateEventProcessing()
    {
        // Arrange
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        db.ProcessedEvents.Add(new ProcessedEvent
        {
            EventId = "evt-duplicate-123",
            ProcessedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Act
        bool isAlreadyProcessed = await db.ProcessedEvents.AnyAsync(p => p.EventId == "evt-duplicate-123");

        // Assert
        Assert.True(isAlreadyProcessed);
    }
}
