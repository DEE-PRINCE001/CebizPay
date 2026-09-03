using CebizPay.Domain.Communication.Enums;

namespace CebizPay.Domain.Communication.Entities;

/// <summary>
/// Domain entity representing an authenticated user's registered FCM device token.
/// Sensitive infrastructure identifier used exclusively for push dispatch.
/// </summary>
public class DeviceToken
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Owner user identifier.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>FCM registration token. Sensitive token, never logged.</summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>Target client device platform (Android, iOS, Web).</summary>
    public DevicePlatform Platform { get; private set; }

    /// <summary>Optional client device model or browser description.</summary>
    public string? DeviceModel { get; private set; }

    /// <summary>Active status flag. False when expired or unregistered.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last updated timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Last used timestamp when a push was sent to this device.</summary>
    public DateTime? LastUsedAtUtc { get; private set; }

    private DeviceToken() { } // EF Core

    /// <summary>
    /// Factory method to register a new device token.
    /// </summary>
    public static DeviceToken Create(
        string userId,
        string token,
        DevicePlatform platform,
        string? deviceModel = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token is required.", nameof(token));
        }

        return new DeviceToken
        {
            Id = Guid.NewGuid(),
            UserId = userId.Trim(),
            Token = token.Trim(),
            Platform = platform,
            DeviceModel = string.IsNullOrWhiteSpace(deviceModel) ? null : deviceModel.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Deactivates this device token (e.g. user logged out or FCM reported token unregistered/expired).
    /// </summary>
    public void Deactivate(DateTime now)
    {
        IsActive = false;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Reactivates this device token (e.g. user re-registers the same token).
    /// </summary>
    public void Activate(string userId, DateTime now, string? deviceModel = null)
    {
        UserId = userId.Trim();
        IsActive = true;
        if (!string.IsNullOrWhiteSpace(deviceModel))
        {
            DeviceModel = deviceModel.Trim();
        }
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Records that a push notification was successfully dispatched to this device.
    /// </summary>
    public void RecordUsed(DateTime now)
    {
        LastUsedAtUtc = now;
        UpdatedAtUtc = now;
    }
}
