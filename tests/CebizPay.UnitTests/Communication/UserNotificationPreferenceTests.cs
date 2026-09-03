using CebizPay.Domain.Communication.Entities;
using CebizPay.Domain.Communication.Enums;
using Xunit;

namespace CebizPay.UnitTests.Communication;

public class UserNotificationPreferenceTests
{
    [Theory]
    [InlineData(NotificationType.SecurityAlert, true)]
    [InlineData(NotificationType.OrganizationSuspended, true)]
    [InlineData(NotificationType.LoanApproved, false)]
    [InlineData(NotificationType.PayrollCompleted, false)]
    [InlineData(NotificationType.ThriftDelinquency, false)]
    [InlineData(NotificationType.PlatformAnnouncement, false)]
    [InlineData(NotificationType.WorkplaceAnnouncement, false)]
    public void IsMandatoryCategory_EvaluatesCorrectly(NotificationType type, bool expectedMandatory)
    {
        var result = UserNotificationPreference.IsMandatoryCategory(type);
        Assert.Equal(expectedMandatory, result);
    }

    [Fact]
    public void CreateDefault_MandatoryCategory_EnablesAllChannels()
    {
        var pref = UserNotificationPreference.CreateDefault("user-1", NotificationType.SecurityAlert);

        Assert.True(pref.InAppEnabled);
        Assert.True(pref.PushEnabled);
        Assert.True(pref.EmailEnabled);
        Assert.True(pref.SmsEnabled);
    }

    [Fact]
    public void Update_MandatoryCategory_PreventsDisablingMandatoryChannels()
    {
        var pref = UserNotificationPreference.CreateDefault("user-1", NotificationType.SecurityAlert);

        // Attempt to disable push, email, sms
        pref.Update(inApp: false, push: false, email: false, sms: false, DateTime.UtcNow);

        // InApp, Push, Email, SMS must remain true for SecurityAlert
        Assert.True(pref.InAppEnabled);
        Assert.True(pref.PushEnabled);
        Assert.True(pref.EmailEnabled);
        Assert.True(pref.SmsEnabled);
    }

    [Fact]
    public void Update_NonMandatoryCategory_AppliesChanges()
    {
        var pref = UserNotificationPreference.CreateDefault("user-1", NotificationType.LoanApproved);

        pref.Update(inApp: true, push: false, email: false, sms: false, DateTime.UtcNow);

        Assert.True(pref.InAppEnabled);
        Assert.False(pref.PushEnabled);
        Assert.False(pref.EmailEnabled);
        Assert.False(pref.SmsEnabled);
    }
}
