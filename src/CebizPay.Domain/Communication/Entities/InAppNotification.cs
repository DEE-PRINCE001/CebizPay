using CebizPay.Domain.Communication.Enums;

namespace CebizPay.Domain.Communication.Entities;

/// <summary>
/// Domain entity representing a durable in-app notification delivered to a user's notification center.
/// Supports tenant-isolated queries and unread state management.
/// </summary>
public class InAppNotification
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Recipient user identifier.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>
    /// Optional organization identifier for tenant-scoped notifications.
    /// When populated, strictly partitioned to members of that organization.
    /// </summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Domain notification category.</summary>
    public NotificationType Type { get; private set; }

    /// <summary>Notification display title (concise headline).</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Notification display body text.</summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>Delivery urgency and priority level.</summary>
    public NotificationPriority Priority { get; private set; }

    /// <summary>Optional in-app deep link or navigation target route.</summary>
    public string? DeepLink { get; private set; }

    /// <summary>Correlation event identifier for deduplication and audit tracking.</summary>
    public string? EventId { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Timestamp when the user read the notification. Null if unread.</summary>
    public DateTime? ReadAtUtc { get; private set; }

    /// <summary>Optional expiration timestamp after which the notification can be archived.</summary>
    public DateTime? ExpiresAtUtc { get; private set; }

    /// <summary>Convenience flag indicating whether the notification has been read.</summary>
    public bool IsRead => ReadAtUtc.HasValue;

    private InAppNotification() { } // EF Core

    /// <summary>
    /// Factory method to create a new in-app notification.
    /// </summary>
    public static InAppNotification Create(
        string userId,
        Guid? organizationId,
        NotificationType type,
        string title,
        string body,
        NotificationPriority priority = NotificationPriority.Normal,
        string? deepLink = null,
        string? eventId = null,
        DateTime? expiresAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Body is required.", nameof(body));
        }

        return new InAppNotification
        {
            Id = Guid.NewGuid(),
            UserId = userId.Trim(),
            OrganizationId = organizationId,
            Type = type,
            Title = title.Trim().Length > 200 ? title.Trim()[..200] : title.Trim(),
            Body = body.Trim().Length > 2000 ? body.Trim()[..2000] : body.Trim(),
            Priority = priority,
            DeepLink = string.IsNullOrWhiteSpace(deepLink) ? null : deepLink.Trim(),
            EventId = string.IsNullOrWhiteSpace(eventId) ? null : eventId.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    /// <summary>
    /// Marks the notification as read.
    /// </summary>
    public void MarkAsRead(DateTime now)
    {
        if (!ReadAtUtc.HasValue)
        {
            ReadAtUtc = now;
        }
    }

    /// <summary>
    /// Reverts the notification to unread status.
    /// </summary>
    public void MarkAsUnread()
    {
        ReadAtUtc = null;
    }
}
