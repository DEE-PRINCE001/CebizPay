using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Admin.Audit;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Auditing;

public sealed class GetAuditLogsQueryTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        await using var db = CreateDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns((string?)null);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var handler = new GetAuditLogsQueryHandler(db, currentUserService, orgContext);
        var query = new GetAuditLogsQuery();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SuperAdmin_ShouldQueryPlatformWide()
    {
        // Arrange
        await using var db = CreateDbContext();
        var superAdminUserId = "superadmin-1";
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(superAdminUserId);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        // Create SuperAdmin profile
        var adminProfile = new AdminProfile(superAdminUserId, AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(adminProfile);

        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();

        db.AuditLogs.AddRange(
            AuditLog.Create(superAdminUserId, AuditActions.FeePolicyCreated, AuditResourceTypes.FeePolicy, "p1"),
            AuditLog.Create("user-a", AuditActions.PeerTransferCompleted, AuditResourceTypes.PeerTransfer, "t1", organizationId: orgA),
            AuditLog.Create("user-b", AuditActions.PeerTransferCompleted, AuditResourceTypes.PeerTransfer, "t2", organizationId: orgB)
        );
        await db.SaveChangesAsync();

        var handler = new GetAuditLogsQueryHandler(db, currentUserService, orgContext);
        var query = new GetAuditLogsQuery(PageNumber: 1, PageSize: 10);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(1, result.TotalPages);
        Assert.False(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public async Task Handle_AdminWithAuditViewPermission_ShouldFilterBySpecifiedOrg()
    {
        // Arrange
        await using var db = CreateDbContext();
        var adminUserId = "admin-viewer";
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(adminUserId);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var adminProfile = new AdminProfile(adminUserId, AdminRoleType.Admin);
        adminProfile.GrantPermission(Permissions.AuditView);
        db.AdminProfiles.Add(adminProfile);

        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();

        db.AuditLogs.AddRange(
            AuditLog.Create("user-a", AuditActions.PeerTransferCompleted, AuditResourceTypes.PeerTransfer, "t1", organizationId: orgA),
            AuditLog.Create("user-b", AuditActions.PeerTransferCompleted, AuditResourceTypes.PeerTransfer, "t2", organizationId: orgB)
        );
        await db.SaveChangesAsync();

        var handler = new GetAuditLogsQueryHandler(db, currentUserService, orgContext);
        var query = new GetAuditLogsQuery(OrganizationId: orgA);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(orgA, result.Items[0].OrganizationId);
    }

    [Fact]
    public async Task Handle_OrganizationUser_ShouldBeRestrictedToCurrentOrganization()
    {
        // Arrange
        await using var db = CreateDbContext();
        var orgUserId = "org-user-1";
        var orgId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(orgUserId);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        orgContext.CurrentOrganizationId.Returns(orgId);
        orgContext.HasAccessToOrganizationAsync(orgId, Arg.Any<CancellationToken>()).Returns(true);

        db.OrganizationMemberships.Add(new OrganizationMembership(orgUserId, orgId, MembershipRoleType.Owner));
        db.AuditLogs.AddRange(
            AuditLog.Create(orgUserId, AuditActions.PeerTransferCompleted, AuditResourceTypes.PeerTransfer, "t1", organizationId: orgId),
            AuditLog.Create("other-user", AuditActions.PeerTransferCompleted, AuditResourceTypes.PeerTransfer, "t2", organizationId: otherOrgId)
        );
        await db.SaveChangesAsync();

        var handler = new GetAuditLogsQueryHandler(db, currentUserService, orgContext);
        var query = new GetAuditLogsQuery(); // No orgId specified by client

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(orgId, result.Items[0].OrganizationId);
    }

    [Fact]
    public async Task Handle_OrganizationOrdinaryMember_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        await using var db = CreateDbContext();
        var orgUserId = "org-member-1";
        var orgId = Guid.NewGuid();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(orgUserId);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        orgContext.CurrentOrganizationId.Returns(orgId);
        orgContext.HasAccessToOrganizationAsync(orgId, Arg.Any<CancellationToken>()).Returns(true);

        // Member role (NOT Owner or Admin)
        db.OrganizationMemberships.Add(new OrganizationMembership(orgUserId, orgId, MembershipRoleType.Member));
        db.AuditLogs.Add(AuditLog.Create(orgUserId, AuditActions.PeerTransferCompleted, AuditResourceTypes.PeerTransfer, "t1", organizationId: orgId));
        await db.SaveChangesAsync();

        var handler = new GetAuditLogsQueryHandler(db, currentUserService, orgContext);
        var query = new GetAuditLogsQuery();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(query, CancellationToken.None));
        Assert.Contains("permission", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_NonMemberUser_AttemptingToQueryOrg_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        await using var db = CreateDbContext();
        var outsideUserId = "outside-user-1";
        var victimOrgId = Guid.NewGuid();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(outsideUserId);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        orgContext.CurrentOrganizationId.Returns(victimOrgId);
        orgContext.HasAccessToOrganizationAsync(victimOrgId, Arg.Any<CancellationToken>()).Returns(false);

        db.AuditLogs.Add(AuditLog.Create("admin", AuditActions.PeerTransferCompleted, AuditResourceTypes.PeerTransfer, "tx-1", organizationId: victimOrgId));
        await db.SaveChangesAsync();

        var handler = new GetAuditLogsQueryHandler(db, currentUserService, orgContext);
        var query = new GetAuditLogsQuery();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(query, CancellationToken.None));
        Assert.Contains("permission", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_OrganizationUser_AttemptingCrossTenantQuery_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        await using var db = CreateDbContext();
        var orgUserId = "org-user-1";
        var myOrgId = Guid.NewGuid();
        var victimOrgId = Guid.NewGuid();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(orgUserId);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        orgContext.CurrentOrganizationId.Returns(myOrgId);

        var handler = new GetAuditLogsQueryHandler(db, currentUserService, orgContext);
        var query = new GetAuditLogsQuery(OrganizationId: victimOrgId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(query, CancellationToken.None));
        Assert.Contains("Cross-tenant", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_WithMultiAttributeFilters_ShouldFilterCorrectly()
    {
        // Arrange
        await using var db = CreateDbContext();
        var superAdminUserId = "superadmin-1";
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(superAdminUserId);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var adminProfile = new AdminProfile(superAdminUserId, AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(adminProfile);

        var targetCorrId = "corr-unique-999";
        db.AuditLogs.AddRange(
            AuditLog.Create("actor-A", AuditActions.PinSet, AuditResourceTypes.User, "user-1", correlationId: targetCorrId),
            AuditLog.Create("actor-B", AuditActions.PinLocked, AuditResourceTypes.User, "user-2", correlationId: "other-corr")
        );
        await db.SaveChangesAsync();

        var handler = new GetAuditLogsQueryHandler(db, currentUserService, orgContext);
        var query = new GetAuditLogsQuery(Action: AuditActions.PinSet, CorrelationId: targetCorrId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(AuditActions.PinSet, result.Items[0].Action);
        Assert.Equal(targetCorrId, result.Items[0].CorrelationId);
    }
}
