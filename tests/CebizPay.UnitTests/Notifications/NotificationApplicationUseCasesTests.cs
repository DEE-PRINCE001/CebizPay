using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Notifications;
using CebizPay.Domain.Communication.Entities;
using CebizPay.Domain.Communication.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Notifications;

public sealed class NotificationApplicationUseCasesTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetNotifications_ReturnsOnlyCurrentUserNotifications_WithUnreadFilter()
    {
        await using var db = CreateDbContext();
        var user1 = "user-01";
        var user2 = "user-02";

        var n1 = InAppNotification.Create(user1, null, NotificationType.LoanApproved, "Title 1", "Body 1");
        var n2 = InAppNotification.Create(user1, null, NotificationType.SecurityAlert, "Title 2", "Body 2");
        n2.MarkAsRead(DateTime.UtcNow);
        var n3 = InAppNotification.Create(user2, null, NotificationType.PlatformAnnouncement, "Title 3", "Body 3");

        db.InAppNotifications.AddRange(n1, n2, n3);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(user1);
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var handler = new GetNotificationsQueryHandler(db, currentUserService, orgContext);

        // Query all for user1
        var allResult = await handler.Handle(new GetNotificationsQuery(null, null, 1, 10), CancellationToken.None);
        Assert.Equal(2, allResult.TotalCount);

        // Query only unread for user1
        var unreadResult = await handler.Handle(new GetNotificationsQuery(false, null, 1, 10), CancellationToken.None);
        Assert.Equal(1, unreadResult.TotalCount);
        Assert.Equal("Title 1", unreadResult.Items[0].Title);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsAccurateCount()
    {
        await using var db = CreateDbContext();
        var user1 = "user-count";

        var n1 = InAppNotification.Create(user1, null, NotificationType.LoanApproved, "T1", "B1");
        var n2 = InAppNotification.Create(user1, null, NotificationType.PayrollCompleted, "T2", "B2");
        var n3 = InAppNotification.Create(user1, null, NotificationType.SecurityAlert, "T3", "B3");
        n3.MarkAsRead(DateTime.UtcNow);

        db.InAppNotifications.AddRange(n1, n2, n3);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(user1);

        var handler = new GetUnreadNotificationCountQueryHandler(db, currentUserService);
        var count = await handler.Handle(new GetUnreadNotificationCountQuery(), CancellationToken.None);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task MarkNotificationRead_WhenCallerIsOwner_Succeeds()
    {
        await using var db = CreateDbContext();
        var user1 = "user-mark";
        var notification = InAppNotification.Create(user1, null, NotificationType.LoanApproved, "T", "B");
        db.InAppNotifications.Add(notification);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(user1);

        var handler = new MarkNotificationReadCommandHandler(db, currentUserService);
        var result = await handler.Handle(new MarkNotificationReadCommand(notification.Id), CancellationToken.None);

        Assert.True(result);
        var reloaded = await db.InAppNotifications.FindAsync(notification.Id);
        Assert.NotNull(reloaded?.ReadAtUtc);
    }

    [Fact]
    public async Task MarkNotificationRead_WhenNotificationBelongsToAnotherUser_ThrowsKeyNotFoundException()
    {
        await using var db = CreateDbContext();
        var notification = InAppNotification.Create("user-other", null, NotificationType.LoanApproved, "T", "B");
        db.InAppNotifications.Add(notification);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("user-intruder");

        var handler = new MarkNotificationReadCommandHandler(db, currentUserService);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new MarkNotificationReadCommand(notification.Id), CancellationToken.None));
    }

    [Fact]
    public async Task MarkAllNotificationsRead_MarksOnlyCallerUnreadNotifications()
    {
        await using var db = CreateDbContext();
        var user1 = "user-bulk";
        var user2 = "user-bulk-2";

        var n1 = InAppNotification.Create(user1, null, NotificationType.LoanApproved, "T1", "B1");
        var n2 = InAppNotification.Create(user1, null, NotificationType.PayrollCompleted, "T2", "B2");
        var n3 = InAppNotification.Create(user2, null, NotificationType.ThriftDelinquency, "T3", "B3");

        db.InAppNotifications.AddRange(n1, n2, n3);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(user1);

        var handler = new MarkAllNotificationsReadCommandHandler(db, currentUserService);
        var markedCount = await handler.Handle(new MarkAllNotificationsReadCommand(), CancellationToken.None);

        Assert.Equal(2, markedCount);

        var user2Notification = await db.InAppNotifications.FindAsync(n3.Id);
        Assert.Null(user2Notification?.ReadAtUtc);
    }

    [Fact]
    public async Task RegisterDeviceToken_NewToken_RegistersActiveDevice()
    {
        await using var db = CreateDbContext();
        var user = "user-device";

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(user);

        var handler = new RegisterDeviceTokenCommandHandler(db, currentUserService);
        var success = await handler.Handle(new RegisterDeviceTokenCommand("token-xyz", DevicePlatform.Android, "Pixel 9"), CancellationToken.None);

        Assert.True(success);

        var tokenRecord = await db.DeviceTokens.FirstOrDefaultAsync(t => t.Token == "token-xyz");
        Assert.NotNull(tokenRecord);
        Assert.True(tokenRecord.IsActive);
        Assert.Equal(user, tokenRecord.UserId);
        Assert.Equal("Pixel 9", tokenRecord.DeviceModel);
    }

    [Fact]
    public async Task DeactivateDeviceToken_DeactivatesCallerDeviceToken()
    {
        await using var db = CreateDbContext();
        var user = "user-deact";
        var deviceToken = DeviceToken.Create(user, "token-to-deactivate", DevicePlatform.iOS);
        db.DeviceTokens.Add(deviceToken);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(user);

        var handler = new DeactivateDeviceTokenCommandHandler(db, currentUserService);
        await handler.Handle(new DeactivateDeviceTokenCommand("token-to-deactivate"), CancellationToken.None);

        var reloaded = await db.DeviceTokens.FirstOrDefaultAsync(t => t.Token == "token-to-deactivate");
        Assert.NotNull(reloaded);
        Assert.False(reloaded.IsActive);
    }

    [Fact]
    public async Task UpdateNotificationPreferences_EnforcesMandatoryProtection()
    {
        await using var db = CreateDbContext();
        var user = "user-pref";

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(user);

        var handler = new UpdateNotificationPreferencesCommandHandler(db, currentUserService);

        var items = new List<UpdatePreferenceItem>
        {
            // Try to turn off all channels for SecurityAlert
            new(NotificationType.SecurityAlert, PushEnabled: false, EmailEnabled: false, SmsEnabled: false),
            // Turn off Push and Email for LoanApproved
            new(NotificationType.LoanApproved, PushEnabled: false, EmailEnabled: false, SmsEnabled: false)
        };

        var result = await handler.Handle(new UpdateNotificationPreferencesCommand(items), CancellationToken.None);

        var securityPref = result.First(p => p.Type == NotificationType.SecurityAlert);
        Assert.True(securityPref.IsMandatory);
        Assert.True(securityPref.InAppEnabled);
        Assert.True(securityPref.PushEnabled);
        Assert.True(securityPref.EmailEnabled);
        Assert.True(securityPref.SmsEnabled);

        var loanPref = result.First(p => p.Type == NotificationType.LoanApproved);
        Assert.False(loanPref.IsMandatory);
        Assert.False(loanPref.PushEnabled);
        Assert.False(loanPref.EmailEnabled);
    }
}
