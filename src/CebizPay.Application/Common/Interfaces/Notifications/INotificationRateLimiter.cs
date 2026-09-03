using CebizPay.Domain.Communication.Enums;

namespace CebizPay.Application.Common.Interfaces.Notifications;

/// <summary>
/// Rate limiting and anti-flooding guard for outbound notifications (SMS, Email, Push).
/// Protects users and external provider budgets from high-frequency event bursts while always allowing critical alerts.
/// </summary>
public interface INotificationRateLimiter
{
    /// <summary>
    /// Determines whether the recipient has capacity to receive an outbound notification on the specified channel.
    /// Critical priority notifications bypass rate limiting.
    /// </summary>
    Task<bool> ShouldAllowDispatchAsync(
        string recipientId,
        NotificationChannel channel,
        NotificationPriority priority,
        CancellationToken cancellationToken = default);
}
