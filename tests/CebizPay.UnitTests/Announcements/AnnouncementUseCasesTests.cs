using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Announcements;
using CebizPay.Domain.Communication.Entities;
using CebizPay.Domain.Communication.Enums;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Announcements;

public sealed class AnnouncementUseCasesTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Organization CreateTestOrganization(string name = "TechCorp Nigeria", OrganizationStatus status = OrganizationStatus.Verified)
    {
        var org = new Organization(name, $"{name.Replace(" ", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant()}@example.com", "+2348011223344");

        if (status != OrganizationStatus.Pending)
        {
            org.TransitionStatus(OrganizationStatus.Verified);
            if (status == OrganizationStatus.Suspended)
            {
                org.TransitionStatus(OrganizationStatus.Suspended);
            }
        }

        return org;
    }

    [Fact]
    public async Task CreateAnnouncement_AsSuperAdmin_PlatformScope_ShouldSucceedWithNullOrganizationId()
    {
        await using var db = CreateDbContext();
        var superAdminId = "super-admin-01";
        db.AdminProfiles.Add(new AdminProfile(superAdminId, AdminRoleType.SuperAdmin));
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(superAdminId);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var handler = new CreateAnnouncementCommandHandler(db, currentUserService, orgContext);
        var command = new CreateAnnouncementCommand(
            AnnouncementScope.Platform,
            "System Maintenance Notice",
            "Platform will be undergoing scheduled maintenance this weekend.",
            PublishImmediately: true);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Null(result.OrganizationId);
        Assert.Equal(AnnouncementScope.Platform, result.Scope);
        Assert.Equal(AnnouncementStatus.Published, result.Status);
        Assert.NotNull(result.PublishedAtUtc);
        Assert.Equal(superAdminId, result.PublishedByUserId);

        var saved = await db.Announcements.FirstOrDefaultAsync(a => a.Id == result.Id);
        Assert.NotNull(saved);
        Assert.Null(saved.OrganizationId);
        Assert.Equal(AnnouncementStatus.Published, saved.Status);

        var auditLog = await db.AuditLogs.FirstOrDefaultAsync(l => l.ResourceId == result.Id.ToString());
        Assert.NotNull(auditLog);
        Assert.Equal("ANNOUNCEMENT_PUBLISHED", auditLog.Action);
    }

    [Fact]
    public async Task CreateAnnouncement_AsAuditor_PlatformScope_ShouldThrowUnauthorizedAccessException()
    {
        await using var db = CreateDbContext();
        var auditorId = "auditor-01";
        db.AdminProfiles.Add(new AdminProfile(auditorId, AdminRoleType.Auditor));
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(auditorId);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var handler = new CreateAnnouncementCommandHandler(db, currentUserService, orgContext);
        var command = new CreateAnnouncementCommand(
            AnnouncementScope.Platform,
            "Auditor Notice",
            "Auditors cannot publish platform announcements.");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAnnouncement_AsHrManager_WorkplaceScope_ShouldSucceedWithAuthenticatedOrganizationContext()
    {
        await using var db = CreateDbContext();
        var org = CreateTestOrganization();
        db.Organizations.Add(org);

        var hrUserId = "hr-user-01";
        var membership = new OrganizationMembership(hrUserId, org.Id, MembershipRoleType.HrManager);
        db.OrganizationMemberships.Add(membership);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(hrUserId);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        orgContext.CurrentOrganizationId.Returns(org.Id);
        orgContext.IsInOrganizationContext.Returns(true);
        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new CreateAnnouncementCommandHandler(db, currentUserService, orgContext);
        var command = new CreateAnnouncementCommand(
            AnnouncementScope.Workplace,
            "Public Holiday Notice",
            "Offices will be closed for public holiday on Monday.",
            PublishImmediately: true);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(org.Id, result.OrganizationId);
        Assert.Equal(AnnouncementScope.Workplace, result.Scope);
        Assert.Equal(AnnouncementStatus.Published, result.Status);
        Assert.Equal(org.CompanyName, result.OrganizationName);

        var saved = await db.Announcements.FirstOrDefaultAsync(a => a.Id == result.Id);
        Assert.NotNull(saved);
        Assert.Equal(org.Id, saved.OrganizationId);
    }

    [Fact]
    public async Task CreateAnnouncement_AsOrdinaryStaffMember_WorkplaceScope_ShouldThrowUnauthorizedAccessException()
    {
        await using var db = CreateDbContext();
        var org = CreateTestOrganization();
        db.Organizations.Add(org);

        var staffUserId = "staff-user-01";
        var membership = new OrganizationMembership(staffUserId, org.Id, MembershipRoleType.Member);
        db.OrganizationMemberships.Add(membership);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(staffUserId);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        orgContext.CurrentOrganizationId.Returns(org.Id);
        orgContext.IsInOrganizationContext.Returns(true);
        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new CreateAnnouncementCommandHandler(db, currentUserService, orgContext);
        var command = new CreateAnnouncementCommand(
            AnnouncementScope.Workplace,
            "Staff Announcement",
            "Ordinary staff members cannot publish workplace announcements.");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAnnouncement_WorkplaceScope_WhenOrganizationSuspended_ShouldThrowInvalidOperationException()
    {
        await using var db = CreateDbContext();
        var org = CreateTestOrganization(status: OrganizationStatus.Suspended);
        db.Organizations.Add(org);

        var hrUserId = "hr-user-02";
        var membership = new OrganizationMembership(hrUserId, org.Id, MembershipRoleType.HrManager);
        db.OrganizationMemberships.Add(membership);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(hrUserId);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        orgContext.CurrentOrganizationId.Returns(org.Id);
        orgContext.IsInOrganizationContext.Returns(true);
        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new CreateAnnouncementCommandHandler(db, currentUserService, orgContext);
        var command = new CreateAnnouncementCommand(
            AnnouncementScope.Workplace,
            "Title",
            "Description");

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task PublishAnnouncement_DraftToPublished_ShouldUpdateStatusAndLogAudit()
    {
        await using var db = CreateDbContext();
        var superAdminId = "super-admin-01";
        db.AdminProfiles.Add(new AdminProfile(superAdminId, AdminRoleType.SuperAdmin));

        var draft = Announcement.CreatePlatform("Upcoming Feature", "Details coming soon.", superAdminId);
        db.Announcements.Add(draft);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(superAdminId);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var handler = new PublishAnnouncementCommandHandler(db, currentUserService, orgContext);
        var command = new PublishAnnouncementCommand(draft.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(AnnouncementStatus.Published, result.Status);
        Assert.NotNull(result.PublishedAtUtc);
        Assert.Equal(superAdminId, result.PublishedByUserId);

        var auditLog = await db.AuditLogs.FirstOrDefaultAsync(l => l.ResourceId == draft.Id.ToString() && l.Action == "ANNOUNCEMENT_PUBLISHED");
        Assert.NotNull(auditLog);
    }

    [Fact]
    public async Task ArchiveAnnouncement_ShouldHideFromFeeds()
    {
        await using var db = CreateDbContext();
        var superAdminId = "super-admin-01";
        db.AdminProfiles.Add(new AdminProfile(superAdminId, AdminRoleType.SuperAdmin));

        var announcement = Announcement.CreatePlatform("Deprecated Feature", "Feature retired.", superAdminId);
        announcement.Publish(superAdminId, DateTime.UtcNow.AddDays(-10));
        db.Announcements.Add(announcement);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(superAdminId);

        var handler = new ArchiveAnnouncementCommandHandler(db, currentUserService);
        var command = new ArchiveAnnouncementCommand(announcement.Id);

        var result = await handler.Handle(command, CancellationToken.None);
        Assert.True(result);

        var archived = await db.Announcements.FirstOrDefaultAsync(a => a.Id == announcement.Id);
        Assert.Equal(AnnouncementStatus.Archived, archived!.Status);
        Assert.NotNull(archived.ArchivedAtUtc);
    }

    [Fact]
    public async Task GetPlatformAnnouncements_ShouldReturnOnlyPublishedPlatformAnnouncements()
    {
        await using var db = CreateDbContext();
        var org = CreateTestOrganization();
        db.Organizations.Add(org);

        var pubPlatform1 = Announcement.CreatePlatform("Platform 1", "Desc 1", "admin");
        pubPlatform1.Publish("admin", DateTime.UtcNow.AddHours(-2));

        var pubPlatform2 = Announcement.CreatePlatform("Platform 2", "Desc 2", "admin");
        pubPlatform2.Publish("admin", DateTime.UtcNow.AddHours(-1));

        var draftPlatform = Announcement.CreatePlatform("Platform Draft", "Desc", "admin");

        var archivedPlatform = Announcement.CreatePlatform("Platform Archived", "Desc", "admin");
        archivedPlatform.Archive("admin", DateTime.UtcNow);

        var pubWorkplace = Announcement.CreateWorkplace(org.Id, "Workplace 1", "Desc", "hr");
        pubWorkplace.Publish("hr", DateTime.UtcNow);

        db.Announcements.AddRange(pubPlatform1, pubPlatform2, draftPlatform, archivedPlatform, pubWorkplace);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("any-authenticated-user");

        var handler = new GetPlatformAnnouncementsQueryHandler(db, currentUserService);
        var query = new GetPlatformAnnouncementsQuery(1, 20);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item =>
        {
            Assert.Equal(AnnouncementScope.Platform, item.Scope);
            Assert.Equal(AnnouncementStatus.Published, item.Status);
            Assert.Null(item.OrganizationId);
        });

        // Deterministic ordering check: Latest published first
        Assert.Equal("Platform 2", result.Items[0].Title);
        Assert.Equal("Platform 1", result.Items[1].Title);
    }

    [Fact]
    public async Task GetWorkplaceAnnouncements_ShouldEnforceTenantIsolation()
    {
        await using var db = CreateDbContext();
        var orgA = CreateTestOrganization("Org A");
        var orgB = CreateTestOrganization("Org B");
        db.Organizations.AddRange(orgA, orgB);

        var pubOrgA1 = Announcement.CreateWorkplace(orgA.Id, "Org A Announcement 1", "Desc", "hrA");
        pubOrgA1.Publish("hrA", DateTime.UtcNow.AddHours(-3));

        var pubOrgA2 = Announcement.CreateWorkplace(orgA.Id, "Org A Announcement 2", "Desc", "hrA");
        pubOrgA2.Publish("hrA", DateTime.UtcNow.AddHours(-1));

        var draftOrgA = Announcement.CreateWorkplace(orgA.Id, "Org A Draft", "Desc", "hrA");

        var pubOrgB = Announcement.CreateWorkplace(orgB.Id, "Org B Announcement", "Desc", "hrB");
        pubOrgB.Publish("hrB", DateTime.UtcNow);

        var pubPlatform = Announcement.CreatePlatform("Platform News", "Desc", "superadmin");
        pubPlatform.Publish("superadmin", DateTime.UtcNow);

        db.Announcements.AddRange(pubOrgA1, pubOrgA2, draftOrgA, pubOrgB, pubPlatform);
        await db.SaveChangesAsync();

        var userOrgA = "user-org-a";
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(userOrgA);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        orgContext.CurrentOrganizationId.Returns(orgA.Id);
        orgContext.HasAccessToOrganizationAsync(orgA.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new GetWorkplaceAnnouncementsQueryHandler(db, currentUserService, orgContext);
        var query = new GetWorkplaceAnnouncementsQuery(1, 20);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item =>
        {
            Assert.Equal(AnnouncementScope.Workplace, item.Scope);
            Assert.Equal(orgA.Id, item.OrganizationId);
            Assert.Equal(AnnouncementStatus.Published, item.Status);
        });

        // Verify Org B announcements and Platform announcements were completely excluded
        Assert.DoesNotContain(result.Items, i => i.OrganizationId == orgB.Id);
        Assert.DoesNotContain(result.Items, i => i.Scope == AnnouncementScope.Platform);
    }

    [Fact]
    public async Task GetAnnouncementById_CrossTenantIdorAttempt_ShouldThrowKeyNotFoundException()
    {
        await using var db = CreateDbContext();
        var orgA = CreateTestOrganization("Org A");
        var orgB = CreateTestOrganization("Org B");
        db.Organizations.AddRange(orgA, orgB);

        var orgBAnnouncement = Announcement.CreateWorkplace(orgB.Id, "Confidential Org B Notice", "Secret plans", "hrB");
        orgBAnnouncement.Publish("hrB", DateTime.UtcNow);
        db.Announcements.Add(orgBAnnouncement);
        await db.SaveChangesAsync();

        var userOrgA = "user-org-a";
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(userOrgA);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        orgContext.CurrentOrganizationId.Returns(orgA.Id); // Caller is in Org A
        orgContext.HasAccessToOrganizationAsync(orgA.Id, Arg.Any<CancellationToken>()).Returns(true);
        orgContext.HasAccessToOrganizationAsync(orgB.Id, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new GetAnnouncementByIdQueryHandler(db, currentUserService, orgContext);
        var query = new GetAnnouncementByIdQuery(orgBAnnouncement.Id);

        // Crucial invariant: Throws KeyNotFoundException to prevent leaking whether Org B's announcement exists
        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(query, CancellationToken.None));
    }
}
