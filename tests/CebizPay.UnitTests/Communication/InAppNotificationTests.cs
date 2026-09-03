using CebizPay.Domain.Communication.Entities;
using CebizPay.Domain.Communication.Enums;
using Xunit;

namespace CebizPay.UnitTests.Communication;

public class InAppNotificationTests
{
    [Fact]
    public void Create_ValidParameters_InstantiatesSuccessfully()
    {
        var userId = "user-123";
        var orgId = Guid.NewGuid();
        var type = NotificationType.LoanApproved;
        var title = "Loan Approved";
        var body = "Your loan has been approved.";
        var priority = NotificationPriority.High;
        var deepLink = "/loans/123";
        var eventId = "loan-evt-1";

        var notification = InAppNotification.Create(
            userId,
            orgId,
            type,
            title,
            body,
            priority,
            deepLink,
            eventId);

        Assert.NotEqual(Guid.Empty, notification.Id);
        Assert.Equal(userId, notification.UserId);
        Assert.Equal(orgId, notification.OrganizationId);
        Assert.Equal(type, notification.Type);
        Assert.Equal(title, notification.Title);
        Assert.Equal(body, notification.Body);
        Assert.Equal(priority, notification.Priority);
        Assert.Equal(deepLink, notification.DeepLink);
        Assert.Equal(eventId, notification.EventId);
        Assert.Null(notification.ReadAtUtc);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public void MarkAsRead_WhenUnread_UpdatesReadAtUtc()
    {
        var notification = InAppNotification.Create(
            "user-1", null, NotificationType.SecurityAlert, "Alert", "Security event", NotificationPriority.Critical);

        var now = DateTime.UtcNow;
        notification.MarkAsRead(now);

        Assert.True(notification.IsRead);
        Assert.Equal(now, notification.ReadAtUtc);
    }

    [Fact]
    public void MarkAsRead_WhenAlreadyRead_DoesNotOverwriteEarlierTimestamp()
    {
        var notification = InAppNotification.Create(
            "user-1", null, NotificationType.SecurityAlert, "Alert", "Security event", NotificationPriority.Critical);

        var firstRead = DateTime.UtcNow.AddMinutes(-10);
        notification.MarkAsRead(firstRead);

        var secondRead = DateTime.UtcNow;
        notification.MarkAsRead(secondRead);

        Assert.Equal(firstRead, notification.ReadAtUtc);
    }

    [Fact]
    public void MarkAsUnread_WhenRead_ClearsReadAtUtc()
    {
        var notification = InAppNotification.Create(
            "user-1", null, NotificationType.SecurityAlert, "Alert", "Security event", NotificationPriority.Critical);

        notification.MarkAsRead(DateTime.UtcNow);
        Assert.True(notification.IsRead);

        notification.MarkAsUnread();
        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAtUtc);
    }

    [Theory]
    [InlineData("", "Title", "Body")]
    [InlineData("   ", "Title", "Body")]
    [InlineData("user-1", "", "Body")]
    [InlineData("user-1", "Title", "")]
    public void Create_InvalidArguments_ThrowsArgumentException(string userId, string title, string body)
    {
        Assert.Throws<ArgumentException>(() => InAppNotification.Create(
            userId, null, NotificationType.PlatformAnnouncement, title, body));
    }
}
