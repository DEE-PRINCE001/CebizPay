using CebizPay.Domain.Communication.Enums;

namespace CebizPay.Application.Common.Interfaces.Notifications;

/// <summary>
/// Delivery payload provided to a notification channel.
/// </summary>
public sealed record NotificationPayload(
    string EventId,
    string RecipientUserId,
    string? RecipientEmail,
    string? RecipientPhoneNumber,
    Guid? OrganizationId,
    NotificationType Type,
    NotificationPriority Priority,
    string Title,
    string Body,
    string? DeepLink,
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>
/// Result of a channel delivery attempt.
/// </summary>
public sealed record NotificationDeliveryResult(
    NotificationChannel Channel,
    NotificationDeliveryStatus Status,
    string? FailureReason = null);

/// <summary>
/// Common abstraction for provider-neutral delivery channels.
/// </summary>
public interface INotificationChannel
{
    /// <summary>Gets the channel identifier.</summary>
    NotificationChannel Channel { get; }

    /// <summary>
    /// Dispatches a notification payload through this channel asynchronously.
    /// </summary>
    Task<NotificationDeliveryResult> DispatchAsync(NotificationPayload payload, CancellationToken cancellationToken = default);
}

/// <summary>In-app notification channel.</summary>
public interface IInAppNotificationChannel : INotificationChannel { }

/// <summary>Push notification channel (FCM).</summary>
public interface IPushNotificationChannel : INotificationChannel { }

/// <summary>Email notification channel.</summary>
public interface IEmailNotificationChannel : INotificationChannel { }

/// <summary>SMS notification channel.</summary>
public interface ISmsNotificationChannel : INotificationChannel { }
