using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoRide.Notification.Data;
using GoRide.Notification.Models.Entities;

namespace GoRide.Notification.Controllers;

/// <summary>
/// Controller for managing rider FCM device token registrations.
/// Allows mobile and web clients to register push notification tokens.
/// </summary>
[ApiController]
[Route("api/notifications/device-token")]
public class DeviceTokenController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<DeviceTokenController> _logger;

    public DeviceTokenController(AppDbContext db, ILogger<DeviceTokenController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Registers or updates an FCM push token for a specific rider.
    /// </summary>
    /// <param name="request">Device token registration payload.</param>
    /// <returns>HTTP 200 OK on success.</returns>
    [HttpPost]
    public async Task<IActionResult> RegisterToken([FromBody] RegisterDeviceTokenRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var existing = await _db.DeviceTokens
            .FirstOrDefaultAsync(t => t.RiderId == request.RiderId && t.Token == request.Token);

        if (existing != null)
        {
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.DeviceTokens.Add(new DeviceToken
            {
                RiderId = request.RiderId,
                Token = request.Token,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Registered FCM device token for rider {RiderId}", request.RiderId);

        return Ok(new { status = "success", message = "Device token registered successfully" });
    }
}

/// <summary>
/// Request payload for device token registration.
/// </summary>
public class RegisterDeviceTokenRequest
{
    /// <summary>
    /// Rider ID associated with the device token.
    /// </summary>
    [Required]
    public string RiderId { get; set; } = default!;

    /// <summary>
    /// FCM registration token string.
    /// </summary>
    [Required]
    public string Token { get; set; } = default!;
}
