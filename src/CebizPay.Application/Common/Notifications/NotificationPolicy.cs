using CebizPay.Application.Common.Interfaces.Notifications;
using CebizPay.Domain.Communication.Entities;
using CebizPay.Domain.Communication.Enums;

namespace CebizPay.Application.Common.Notifications;

/// <summary>
/// Authoritative notification policy implementing default channel selection and preference evaluation.
/// Follows the PRD and Engineering Specifications channel matrix.
/// </summary>
public sealed class NotificationPolicy : INotificationPolicy
{
    /// <inheritdoc/>
    public IReadOnlyList<NotificationChannel> ResolveEligibleChannels(
        NotificationType type,
        NotificationPriority priority,
        UserNotificationPreference? userPreference = null)
    {
        // 1. Determine baseline allowed channels per category and priority
        var baselineChannels = type switch
        {
            NotificationType.OrganizationSuspended => new[]
            {
                NotificationChannel.InApp,
                NotificationChannel.Push,
                NotificationChannel.Email,
                NotificationChannel.Sms
            },

            NotificationType.SecurityAlert => new[]
            {
                NotificationChannel.InApp,
                NotificationChannel.Push,
                NotificationChannel.Email,
                NotificationChannel.Sms
            },

            NotificationType.LoanApproved => new[]
            {
                NotificationChannel.InApp,
                NotificationChannel.Push,
                NotificationChannel.Email
            },

            NotificationType.PayrollCompleted => new[]
            {
                NotificationChannel.InApp,
                NotificationChannel.Push,
                NotificationChannel.Email
            },

            NotificationType.ThriftDelinquency => new[]
            {
                NotificationChannel.InApp,
                NotificationChannel.Push,
                NotificationChannel.Email
            },

            NotificationType.PlatformAnnouncement => new[]
            {
                NotificationChannel.InApp,
                NotificationChannel.Push
            },

            NotificationType.WorkplaceAnnouncement => new[]
            {
                NotificationChannel.InApp,
                NotificationChannel.Push
            },

            _ => priority == NotificationPriority.Critical
                ? new[] { NotificationChannel.InApp, NotificationChannel.Push, NotificationChannel.Email, NotificationChannel.Sms }
                : new[] { NotificationChannel.InApp }
        };

        // 2. Mandatory categories (Security & Organization Suspension) CANNOT be suppressed by user preferences
        if (UserNotificationPreference.IsMandatoryCategory(type) || priority == NotificationPriority.Critical)
        {
            return baselineChannels;
        }

        // 3. For configurable categories, apply user preferences if present
        if (userPreference == null)
        {
            return baselineChannels;
        }

        var eligible = new List<NotificationChannel>();
        foreach (var channel in baselineChannels)
        {
            if (channel == NotificationChannel.InApp || userPreference.IsChannelEnabled(channel))
            {
                eligible.Add(channel);
            }
        }

        return eligible;
    }
}
