using CebizPay.Domain.Communication.Enums;

namespace CebizPay.Domain.Communication.Entities;

/// <summary>
/// Authoritative PostgreSQL delivery log and deduplication ledger record.
/// Enforces exactly-once channel dispatch semantics per event, recipient, notification type, and channel.
/// </summary>
public class NotificationDeliveryRecord
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Originating event identifier (e.g. RabbitMQ event ID or entity GUID).</summary>
    public string EventId { get; private set; } = string.Empty;

    /// <summary>Recipient identifier (e.g. UserId or target phone/email).</summary>
    public string RecipientId { get; private set; } = string.Empty;

    /// <summary>Domain notification category.</summary>
    public NotificationType Type { get; private set; }

    /// <summary>Delivery channel targeted.</summary>
    public NotificationChannel Channel { get; private set; }

    /// <summary>Delivery outcome status.</summary>
    public NotificationDeliveryStatus Status { get; private set; }

    /// <summary>Optional error message or provider failure reason.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Timestamp when the dispatch was attempted.</summary>
    public DateTime AttemptedAtUtc { get; private set; }

    private NotificationDeliveryRecord() { } // EF Core

    /// <summary>
    /// Creates a new delivery record.
    /// </summary>
    public static NotificationDeliveryRecord Create(
        string eventId,
        string recipientId,
        NotificationType type,
        NotificationChannel channel,
        NotificationDeliveryStatus status,
        string? failureReason = null,
        DateTime? attemptedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ArgumentException("EventId is required.", nameof(eventId));
        }

        if (string.IsNullOrWhiteSpace(recipientId))
        {
            throw new ArgumentException("RecipientId is required.", nameof(recipientId));
        }

        return new NotificationDeliveryRecord
        {
            Id = Guid.NewGuid(),
            EventId = eventId.Trim(),
            RecipientId = recipientId.Trim(),
            Type = type,
            Channel = channel,
            Status = status,
            FailureReason = failureReason,
            AttemptedAtUtc = attemptedAtUtc ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// Updates status and failure reason on retry or final outcome.
    /// </summary>
    public void UpdateStatus(NotificationDeliveryStatus status, string? failureReason = null)
    {
        Status = status;
        FailureReason = failureReason;
    }
}
