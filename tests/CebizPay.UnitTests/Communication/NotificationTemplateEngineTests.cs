using CebizPay.Application.Common.Notifications;
using CebizPay.Domain.Communication.Enums;
using Xunit;

namespace CebizPay.UnitTests.Communication;

public class NotificationTemplateEngineTests
{
    private readonly NotificationTemplateEngine _engine = new();

    [Fact]
    public void Render_LoanApproved_SubstitutesAmountAndCurrency()
    {
        var parameters = new Dictionary<string, string>
        {
            ["Amount"] = "250,000.00",
            ["Currency"] = "NGN"
        };

        var rendered = _engine.Render(NotificationType.LoanApproved, NotificationChannel.InApp, parameters);

        Assert.Equal("Loan Application Approved", rendered.Title);
        Assert.Contains("250,000.00", rendered.Body);
        Assert.Contains("NGN", rendered.Body);
        Assert.Equal("/loans", rendered.DeepLink);
    }

    [Fact]
    public void Render_PayrollCompleted_SubstitutesBatchReference()
    {
        var parameters = new Dictionary<string, string>
        {
            ["BatchReference"] = "PAY-2026-AUG"
        };

        var rendered = _engine.Render(NotificationType.PayrollCompleted, NotificationChannel.Push, parameters);

        Assert.Equal("Payroll Batch Completed", rendered.Title);
        Assert.Contains("PAY-2026-AUG", rendered.Body);
        Assert.Equal("/payroll", rendered.DeepLink);
    }

    [Fact]
    public void Render_SecurityAlert_RendersAccurately()
    {
        var rendered = _engine.Render(NotificationType.SecurityAlert, NotificationChannel.Sms, new Dictionary<string, string>());

        Assert.Equal("Security Alert", rendered.Title);
        Assert.Contains("security-sensitive event", rendered.Body);
        Assert.Equal("/security", rendered.DeepLink);
    }

    [Fact]
    public void Render_PlatformAnnouncement_SubstitutesCustomTitleAndDescription()
    {
        var parameters = new Dictionary<string, string>
        {
            ["Title"] = "Scheduled Maintenance Window",
            ["Description"] = "We will be performing routine upgrades this Sunday at midnight."
        };

        var rendered = _engine.Render(NotificationType.PlatformAnnouncement, NotificationChannel.InApp, parameters);

        Assert.Equal("Scheduled Maintenance Window", rendered.Title);
        Assert.Equal("We will be performing routine upgrades this Sunday at midnight.", rendered.Body);
        Assert.Equal("/announcements", rendered.DeepLink);
    }
}
