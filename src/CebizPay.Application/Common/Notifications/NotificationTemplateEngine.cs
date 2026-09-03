using CebizPay.Application.Common.Interfaces.Notifications;
using CebizPay.Domain.Communication.Enums;

namespace CebizPay.Application.Common.Notifications;

/// <summary>
/// Safe, deterministic template renderer for system notifications.
/// Protects against template injection attacks by using strict string interpolation over known keys.
/// </summary>
public sealed class NotificationTemplateEngine : INotificationTemplateEngine
{
    /// <inheritdoc/>
    public RenderedNotification Render(
        NotificationType type,
        NotificationChannel channel,
        IReadOnlyDictionary<string, string> parameters)
    {
        parameters ??= new Dictionary<string, string>();

        string GetParam(string key, string fallback = "") =>
            parameters.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val) ? val : fallback;

        return type switch
        {
            NotificationType.OrganizationSuspended => new RenderedNotification(
                Title: "Organization Access Suspended",
                Body: "Your organization's access has been temporarily restricted. Please contact platform compliance for assistance.",
                DeepLink: "/settings/compliance"),

            NotificationType.LoanApproved => new RenderedNotification(
                Title: "Loan Application Approved",
                Body: !string.IsNullOrWhiteSpace(GetParam("Amount"))
                    ? $"Your loan application for {GetParam("Amount")} {GetParam("Currency", "NGN")} has been approved."
                    : "Your loan application has been approved and funds will be credited shortly.",
                DeepLink: "/loans"),

            NotificationType.PayrollCompleted => new RenderedNotification(
                Title: "Payroll Batch Completed",
                Body: !string.IsNullOrWhiteSpace(GetParam("BatchReference"))
                    ? $"Payroll batch {GetParam("BatchReference")} has completed processing successfully."
                    : "Your organization's scheduled payroll batch has completed processing.",
                DeepLink: "/payroll"),

            NotificationType.ThriftDelinquency => new RenderedNotification(
                Title: "Missed Thrift Contribution",
                Body: !string.IsNullOrWhiteSpace(GetParam("Amount"))
                    ? $"Your scheduled thrift contribution of {GetParam("Amount")} {GetParam("Currency", "NGN")} was missed. Please fund your wallet to avoid penalties."
                    : "A scheduled thrift contribution was missed. Please fund your wallet to avoid suspension.",
                DeepLink: "/thrift"),

            NotificationType.PlatformAnnouncement => new RenderedNotification(
                Title: GetParam("Title", "Platform Announcement"),
                Body: GetParam("Description", "A new platform announcement has been published."),
                DeepLink: "/announcements"),

            NotificationType.WorkplaceAnnouncement => new RenderedNotification(
                Title: GetParam("Title", "Workplace Announcement"),
                Body: GetParam("Description", "A new workplace announcement has been published for your organization."),
                DeepLink: "/announcements/workplace"),

            NotificationType.SecurityAlert => new RenderedNotification(
                Title: GetParam("Title", "Security Alert"),
                Body: GetParam("Body", "A security-sensitive event occurred on your account. If this was not you, contact security immediately."),
                DeepLink: "/security"),

            _ => new RenderedNotification(
                Title: GetParam("Title", "Notification"),
                Body: GetParam("Body", "You have a new notification."),
                DeepLink: null)
        };
    }
}
