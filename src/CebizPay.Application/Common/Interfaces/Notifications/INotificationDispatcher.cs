using CebizPay.Domain.Communication.Enums;

namespace CebizPay.Application.Common.Interfaces.Notifications;

/// <summary>
/// Request to dispatch a multi-channel notification.
/// </summary>
public sealed record DispatchNotificationRequest(
    string EventId,
    string RecipientUserId,
    string? RecipientEmail,
    string? RecipientPhoneNumber,
    Guid? OrganizationId,
    NotificationType Type,
    NotificationPriority Priority,
    IReadOnlyDictionary<string, string> TemplateParameters,
    DateTime? ExpiresAtUtc = null);

/// <summary>
/// Result of multi-channel notification dispatch.
/// </summary>
public sealed record MultiChannelDispatchResult(
    string EventId,
    string RecipientUserId,
    NotificationType Type,
    IReadOnlyList<NotificationDeliveryResult> ChannelResults);

/// <summary>
/// Orchestrator service that applies policy, templates, deduplication, rate limits, and dispatches across channels.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Executes safe asynchronous multi-channel dispatch for a single recipient.
    /// </summary>
    Task<MultiChannelDispatchResult> DispatchAsync(
        DispatchNotificationRequest request,
        CancellationToken cancellationToken = default);
}
