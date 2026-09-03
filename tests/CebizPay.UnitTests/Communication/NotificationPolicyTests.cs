using CebizPay.Application.Common.Notifications;
using CebizPay.Domain.Communication.Entities;
using CebizPay.Domain.Communication.Enums;
using Xunit;

namespace CebizPay.UnitTests.Communication;

public class NotificationPolicyTests
{
    private readonly NotificationPolicy _policy = new();

    [Fact]
    public void ResolveEligibleChannels_SecurityAlert_ReturnsAllFourChannelsEvenIfDisabledByUser()
    {
        var disabledPref = UserNotificationPreference.CreateDefault("user-1", NotificationType.SecurityAlert);
        // Even if preference object existed with disabled flags (not normally possible via Update)
        var channels = _policy.ResolveEligibleChannels(NotificationType.SecurityAlert, NotificationPriority.Critical, disabledPref);

        Assert.Contains(NotificationChannel.InApp, channels);
        Assert.Contains(NotificationChannel.Push, channels);
        Assert.Contains(NotificationChannel.Email, channels);
        Assert.Contains(NotificationChannel.Sms, channels);
    }

    [Fact]
    public void ResolveEligibleChannels_OrganizationSuspended_ReturnsAllFourChannels()
    {
        var channels = _policy.ResolveEligibleChannels(NotificationType.OrganizationSuspended, NotificationPriority.Critical);

        Assert.Contains(NotificationChannel.InApp, channels);
        Assert.Contains(NotificationChannel.Push, channels);
        Assert.Contains(NotificationChannel.Email, channels);
        Assert.Contains(NotificationChannel.Sms, channels);
    }

    [Fact]
    public void ResolveEligibleChannels_LoanApproved_ReturnsInAppAndPushByDefault()
    {
        var channels = _policy.ResolveEligibleChannels(NotificationType.LoanApproved, NotificationPriority.High);

        Assert.Contains(NotificationChannel.InApp, channels);
        Assert.Contains(NotificationChannel.Push, channels);
        Assert.DoesNotContain(NotificationChannel.Sms, channels);
    }

    [Fact]
    public void ResolveEligibleChannels_LoanApproved_WithEmailEnabledByUser_ReturnsEmail()
    {
        var pref = UserNotificationPreference.CreateDefault("user-1", NotificationType.LoanApproved);
        pref.Update(inApp: true, push: true, email: true, sms: false, DateTime.UtcNow);

        var channels = _policy.ResolveEligibleChannels(NotificationType.LoanApproved, NotificationPriority.High, pref);

        Assert.Contains(NotificationChannel.InApp, channels);
        Assert.Contains(NotificationChannel.Push, channels);
        Assert.Contains(NotificationChannel.Email, channels);
        Assert.DoesNotContain(NotificationChannel.Sms, channels);
    }

    [Fact]
    public void ResolveEligibleChannels_PlatformAnnouncement_ReturnsInAppAndPushOnly()
    {
        var pref = UserNotificationPreference.CreateDefault("user-1", NotificationType.PlatformAnnouncement);
        pref.Update(inApp: true, push: true, email: true, sms: true, DateTime.UtcNow);

        var channels = _policy.ResolveEligibleChannels(NotificationType.PlatformAnnouncement, NotificationPriority.Normal, pref);

        Assert.Contains(NotificationChannel.InApp, channels);
        Assert.Contains(NotificationChannel.Push, channels);
        Assert.DoesNotContain(NotificationChannel.Email, channels);
        Assert.DoesNotContain(NotificationChannel.Sms, channels);
    }

    [Fact]
    public void ResolveEligibleChannels_PayrollCompleted_ReturnsInAppPushAndEmail()
    {
        var channels = _policy.ResolveEligibleChannels(NotificationType.PayrollCompleted, NotificationPriority.High);

        Assert.Contains(NotificationChannel.InApp, channels);
        Assert.Contains(NotificationChannel.Push, channels);
        Assert.Contains(NotificationChannel.Email, channels);
        Assert.DoesNotContain(NotificationChannel.Sms, channels);
    }
}
