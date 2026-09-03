using CebizPay.Domain.Communication.Entities;
using CebizPay.Domain.Communication.Enums;

namespace CebizPay.Application.Common.Interfaces.Notifications;

/// <summary>
/// Authoritative policy engine for determining eligible channels, user preferences, and criticality rules.
/// </summary>
public interface INotificationPolicy
{
    /// <summary>
    /// Evaluates eligible delivery channels for a notification given its type, priority, and user preferences.
    /// </summary>
    IReadOnlyList<NotificationChannel> ResolveEligibleChannels(
        NotificationType type,
        NotificationPriority priority,
        UserNotificationPreference? userPreference = null);
}
