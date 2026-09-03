using CebizPay.Domain.Communication.Enums;

namespace CebizPay.Application.Common.Interfaces.Notifications;

/// <summary>
/// Service ensuring exactly-once delivery semantics for events and notifications.
/// Backed by PostgreSQL unique constraints with optional Redis acceleration.
/// </summary>
public interface INotificationDeduplicator
{
    /// <summary>
    /// Checks whether the notification has already been processed or is currently in flight.
    /// </summary>
    Task<bool> ShouldDispatchAsync(
        string eventId,
        string recipientId,
        NotificationType type,
        NotificationChannel channel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the authoritative delivery outcome in the PostgreSQL deduplication table.
    /// </summary>
    Task RecordOutcomeAsync(
        string eventId,
        string recipientId,
        NotificationType type,
        NotificationChannel channel,
        NotificationDeliveryStatus status,
        string? failureReason = null,
        CancellationToken cancellationToken = default);
}
