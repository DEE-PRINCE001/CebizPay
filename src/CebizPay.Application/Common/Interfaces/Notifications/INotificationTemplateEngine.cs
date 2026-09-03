using CebizPay.Domain.Communication.Enums;

namespace CebizPay.Application.Common.Interfaces.Notifications;

/// <summary>
/// Rendered content structure for a notification.
/// </summary>
public sealed record RenderedNotification(
    string Title,
    string Body,
    string? DeepLink,
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>
/// Application service responsible for deterministic, application-owned template rendering.
/// Prevents untrusted template injection.
/// </summary>
public interface INotificationTemplateEngine
{
    /// <summary>
    /// Renders safe title, body, and navigation links for a given notification type and parameter context.
    /// </summary>
    RenderedNotification Render(
        NotificationType type,
        NotificationChannel channel,
        IReadOnlyDictionary<string, string> parameters);
}
