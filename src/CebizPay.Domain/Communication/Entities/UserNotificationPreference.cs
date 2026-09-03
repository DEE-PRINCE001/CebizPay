using CebizPay.Domain.Communication.Enums;

namespace CebizPay.Domain.Communication.Entities;

/// <summary>
/// Domain entity representing a user's delivery channel preferences for a specific notification type.
/// Enforces immutability on mandatory/security-critical communication.
/// </summary>
public class UserNotificationPreference
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Owner user identifier.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>Target notification category.</summary>
    public NotificationType Type { get; private set; }

    /// <summary>Whether in-app delivery is enabled.</summary>
    public bool InAppEnabled { get; private set; } = true;

    /// <summary>Whether push (FCM) delivery is enabled.</summary>
    public bool PushEnabled { get; private set; } = true;

    /// <summary>Whether email delivery is enabled.</summary>
    public bool EmailEnabled { get; private set; } = true;

    /// <summary>Whether SMS delivery is enabled.</summary>
    public bool SmsEnabled { get; private set; }

    /// <summary>Timestamp of last preference update.</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    private UserNotificationPreference() { } // EF Core

    /// <summary>
    /// Indicates whether this notification type contains mandatory legal or security communication that cannot be disabled.
    /// </summary>
    public static bool IsMandatoryCategory(NotificationType type) =>
        type == NotificationType.SecurityAlert || type == NotificationType.OrganizationSuspended;

    /// <summary>
    /// Creates default notification preferences for a user and category.
    /// </summary>
    public static UserNotificationPreference CreateDefault(string userId, NotificationType type)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }

        var isMandatory = IsMandatoryCategory(type);

        return new UserNotificationPreference
        {
            Id = Guid.NewGuid(),
            UserId = userId.Trim(),
            Type = type,
            InAppEnabled = true,
            PushEnabled = true,
            EmailEnabled = true,
            SmsEnabled = isMandatory,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Updates user delivery preferences, enforcing that mandatory categories cannot be opted out of.
    /// </summary>
    public void Update(bool inApp, bool push, bool email, bool sms, DateTime now)
    {
        if (IsMandatoryCategory(Type))
        {
            // Security alerts and organization suspension cannot be suppressed by user preference
            InAppEnabled = true;
            PushEnabled = true;
            EmailEnabled = true;
            SmsEnabled = true;
        }
        else
        {
            // Durable in-app notification center cannot be disabled (it is the authoritative record)
            InAppEnabled = true;
            PushEnabled = push;
            EmailEnabled = email;
            SmsEnabled = sms;
        }

        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Checks whether a specific delivery channel is enabled for this preference.
    /// </summary>
    public bool IsChannelEnabled(NotificationChannel channel)
    {
        if (IsMandatoryCategory(Type))
        {
            return true;
        }

        return channel switch
        {
            NotificationChannel.InApp => InAppEnabled,
            NotificationChannel.Push => PushEnabled,
            NotificationChannel.Email => EmailEnabled,
            NotificationChannel.Sms => SmsEnabled,
            _ => false
        };
    }
}
