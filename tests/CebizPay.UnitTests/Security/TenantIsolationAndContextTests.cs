using System.Security.Claims;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Interfaces.Support;
using CebizPay.Application.UseCases.Support;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Support.Entities;
using CebizPay.Domain.Support.Enums;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Security;

/// <summary>
/// Comprehensive test suite verifying the Tenant Isolation Matrix,
/// CurrentOrganizationContext server-side validation, header spoofing prevention,
/// and IDOR protections across tenant boundaries.
/// </summary>
public sealed class TenantIsolationAndContextTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static IHttpContextAccessor CreateHttpContextAccessor(string? userId, string? orgHeader = null, string? orgClaim = null)
    {
        var httpContext = new DefaultHttpContext();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId)
            };

            if (!string.IsNullOrWhiteSpace(orgClaim))
            {
                claims.Add(new Claim("OrganizationId", orgClaim));
            }

            var identity = new ClaimsIdentity(claims, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }

        if (!string.IsNullOrWhiteSpace(orgHeader))
        {
            httpContext.Request.Headers["X-Organization-Id"] = orgHeader;
        }

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return accessor;
    }

    [Fact]
    public void CurrentOrganizationId_UnauthenticatedUser_ShouldReturnNull()
    {
        // Arrange
        using var db = CreateDbContext();
        var accessor = CreateHttpContextAccessor(userId: null, orgHeader: Guid.NewGuid().ToString());
        var context = new CurrentOrganizationContext(accessor, db);

        // Act & Assert
        Assert.Null(context.CurrentOrganizationId);
        Assert.False(context.IsInOrganizationContext);
    }

    [Fact]
    public void CurrentOrganizationId_NoHeaderOrClaim_ShouldReturnNull()
    {
        // Arrange
        using var db = CreateDbContext();
        var userId = "user-no-header";
        var accessor = CreateHttpContextAccessor(userId: userId);
        var context = new CurrentOrganizationContext(accessor, db);

        // Act & Assert
        Assert.Null(context.CurrentOrganizationId);
        Assert.False(context.IsInOrganizationContext);
    }

    [Fact]
    public void CurrentOrganizationId_UserBelongsToOrg_WithHeader_ShouldReturnOrgId()
    {
        // Arrange
        using var db = CreateDbContext();
        var userId = "valid-member-1";
        var orgId = Guid.NewGuid();

        db.OrganizationMemberships.Add(new OrganizationMembership(userId, orgId, MembershipRoleType.Member));
        db.SaveChanges();

        var accessor = CreateHttpContextAccessor(userId: userId, orgHeader: orgId.ToString());
        var context = new CurrentOrganizationContext(accessor, db);

        // Act & Assert
        Assert.Equal(orgId, context.CurrentOrganizationId);
        Assert.True(context.IsInOrganizationContext);
    }

    [Fact]
    public void CurrentOrganizationId_UserBelongsToOrg_WithClaim_ShouldReturnOrgId()
    {
        // Arrange
        using var db = CreateDbContext();
        var userId = "valid-member-claim";
        var orgId = Guid.NewGuid();

        db.OrganizationMemberships.Add(new OrganizationMembership(userId, orgId, MembershipRoleType.Member));
        db.SaveChanges();

        var accessor = CreateHttpContextAccessor(userId: userId, orgClaim: orgId.ToString());
        var context = new CurrentOrganizationContext(accessor, db);

        // Act & Assert
        Assert.Equal(orgId, context.CurrentOrganizationId);
        Assert.True(context.IsInOrganizationContext);
    }

    [Fact]
    public void CurrentOrganizationId_SpoofedHeader_UserNotMemberOfTargetOrg_ShouldReturnNull()
    {
        // Arrange: User belongs to OrgA, but sends header for OrgB
        using var db = CreateDbContext();
        var userId = "attacker-user";
        var orgA = Guid.NewGuid();
        var victimOrgB = Guid.NewGuid();

        db.OrganizationMemberships.Add(new OrganizationMembership(userId, orgA, MembershipRoleType.Member));
        db.SaveChanges();

        var accessor = CreateHttpContextAccessor(userId: userId, orgHeader: victimOrgB.ToString());
        var context = new CurrentOrganizationContext(accessor, db);

        // Act & Assert: Must NOT return victimOrgB
        Assert.Null(context.CurrentOrganizationId);
        Assert.False(context.IsInOrganizationContext);
    }

    [Fact]
    public void CurrentOrganizationId_UnknownNonExistentOrgHeader_ShouldReturnNullWithoutError()
    {
        // Arrange
        using var db = CreateDbContext();
        var userId = "random-user";
        var nonExistentOrg = Guid.NewGuid();

        var accessor = CreateHttpContextAccessor(userId: userId, orgHeader: nonExistentOrg.ToString());
        var context = new CurrentOrganizationContext(accessor, db);

        // Act & Assert
        Assert.Null(context.CurrentOrganizationId);
        Assert.False(context.IsInOrganizationContext);
    }

    [Fact]
    public void CurrentOrganizationId_MalformedHeader_ShouldFailSafelyAndReturnNull()
    {
        // Arrange
        using var db = CreateDbContext();
        var userId = "random-user";

        var accessor = CreateHttpContextAccessor(userId: userId, orgHeader: "malformed-not-a-guid!@#$%");
        var context = new CurrentOrganizationContext(accessor, db);

        // Act & Assert
        Assert.Null(context.CurrentOrganizationId);
        Assert.False(context.IsInOrganizationContext);
    }

    [Fact]
    public void CurrentOrganizationId_EmptyGuidHeader_ShouldReturnNull()
    {
        // Arrange
        using var db = CreateDbContext();
        var userId = "random-user";

        var accessor = CreateHttpContextAccessor(userId: userId, orgHeader: Guid.Empty.ToString());
        var context = new CurrentOrganizationContext(accessor, db);

        // Act & Assert
        Assert.Null(context.CurrentOrganizationId);
        Assert.False(context.IsInOrganizationContext);
    }

    [Fact]
    public void CurrentOrganizationId_SuspendedMember_ShouldReturnNull()
    {
        // Arrange: User membership in OrgA is suspended
        using var db = CreateDbContext();
        var userId = "suspended-user";
        var orgA = Guid.NewGuid();

        var membership = new OrganizationMembership(userId, orgA, MembershipRoleType.Member);
        membership.SuspendWorkAccess("Security investigation");
        db.OrganizationMemberships.Add(membership);
        db.SaveChanges();

        var accessor = CreateHttpContextAccessor(userId: userId, orgHeader: orgA.ToString());
        var context = new CurrentOrganizationContext(accessor, db);

        // Act & Assert: Suspended membership must NOT establish active org context
        Assert.Null(context.CurrentOrganizationId);
        Assert.False(context.IsInOrganizationContext);
    }

    [Fact]
    public void CurrentOrganizationId_SuperAdmin_ShouldHaveAccessToAnyOrgHeader()
    {
        // Arrange: SuperAdmin can operate across any organization
        using var db = CreateDbContext();
        var superAdminId = "super-admin-user";
        var targetOrg = Guid.NewGuid();

        db.AdminProfiles.Add(new AdminProfile(superAdminId, AdminRoleType.SuperAdmin));
        db.SaveChanges();

        var accessor = CreateHttpContextAccessor(userId: superAdminId, orgHeader: targetOrg.ToString());
        var context = new CurrentOrganizationContext(accessor, db);

        // Act & Assert
        Assert.Equal(targetOrg, context.CurrentOrganizationId);
        Assert.True(context.IsInOrganizationContext);
    }

    [Fact]
    public async Task HasAccessToOrganizationAsync_Member_ShouldReturnTrue()
    {
        // Arrange
        using var db = CreateDbContext();
        var userId = "user-a";
        var orgA = Guid.NewGuid();

        db.OrganizationMemberships.Add(new OrganizationMembership(userId, orgA, MembershipRoleType.Member));
        db.SaveChanges();

        var accessor = CreateHttpContextAccessor(userId: userId);
        var context = new CurrentOrganizationContext(accessor, db);

        // Act
        var result = await context.HasAccessToOrganizationAsync(orgA);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasAccessToOrganizationAsync_NonMember_ShouldReturnFalse()
    {
        // Arrange
        using var db = CreateDbContext();
        var userId = "user-a";
        var orgB = Guid.NewGuid();

        var accessor = CreateHttpContextAccessor(userId: userId);
        var context = new CurrentOrganizationContext(accessor, db);

        // Act
        var result = await context.HasAccessToOrganizationAsync(orgB);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HasAccessToOrganizationAsync_SuperAdmin_ShouldReturnTrueForAnyOrg()
    {
        // Arrange
        using var db = CreateDbContext();
        var superAdminId = "superadmin-x";
        var anyOrg = Guid.NewGuid();

        db.AdminProfiles.Add(new AdminProfile(superAdminId, AdminRoleType.SuperAdmin));
        db.SaveChanges();

        var accessor = CreateHttpContextAccessor(userId: superAdminId);
        var context = new CurrentOrganizationContext(accessor, db);

        // Act
        var result = await context.HasAccessToOrganizationAsync(anyOrg);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CreateSupportTicket_WithSpoofedOrgHeader_ShouldNotCreateCrossTenantTicket()
    {
        // Arrange: User attempts to create a ticket spoofing victim org
        await using var db = CreateDbContext();
        var callerUserId = "user-caller-1";
        var victimOrgId = Guid.NewGuid();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(callerUserId);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        // Spoofed org is reported as CurrentOrganizationId by mock, but HasAccess is FALSE
        orgContext.CurrentOrganizationId.Returns(victimOrgId);
        orgContext.HasAccessToOrganizationAsync(victimOrgId, Arg.Any<CancellationToken>()).Returns(false);

        var numberGen = Substitute.For<ISupportTicketNumberGenerator>();
        numberGen.GenerateTicketNumber().Returns("TCK-20260903-SPOOF1");

        var outbox = Substitute.For<IOutboxService>();
        var audit = Substitute.For<IAuditLogService>();

        var handler = new CreateSupportTicketCommandHandler(db, currentUserService, orgContext, numberGen, audit, outbox);
        var command = new CreateSupportTicketCommand(
            Category: SupportTicketCategory.WalletOrAccount,
            Subject: "Suspicious Activity",
            Description: "Testing spoofed ticket creation",
            Priority: SupportTicketPriority.Normal);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: Ticket organizationId MUST be null (personal ticket) because caller does not have access to victimOrgId
        Assert.NotNull(result);
        Assert.Null(result.OrganizationId);

        var persistedTicket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == result.Id);
        Assert.NotNull(persistedTicket);
        Assert.Null(persistedTicket.OrganizationId);
    }

    [Fact]
    public async Task CreateSupportTicket_WithValidOrgContext_ShouldCreateOrganizationalTicket()
    {
        // Arrange
        await using var db = CreateDbContext();
        var callerUserId = "user-caller-org";
        var myOrgId = Guid.NewGuid();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(callerUserId);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        orgContext.CurrentOrganizationId.Returns(myOrgId);
        orgContext.HasAccessToOrganizationAsync(myOrgId, Arg.Any<CancellationToken>()).Returns(true);

        var numberGen = Substitute.For<ISupportTicketNumberGenerator>();
        numberGen.GenerateTicketNumber().Returns("TCK-20260903-VALID1");

        var outbox = Substitute.For<IOutboxService>();
        var audit = Substitute.For<IAuditLogService>();

        var handler = new CreateSupportTicketCommandHandler(db, currentUserService, orgContext, numberGen, audit, outbox);
        var command = new CreateSupportTicketCommand(
            Category: SupportTicketCategory.BusinessOrWorkplace,
            Subject: "Invoice Discrepancy",
            Description: "Discrepancy on August invoice",
            Priority: SupportTicketPriority.High);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: Organizational ticket correctly tagged with myOrgId
        Assert.NotNull(result);
        Assert.Equal(myOrgId, result.OrganizationId);

        var persistedTicket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == result.Id);
        Assert.NotNull(persistedTicket);
        Assert.Equal(myOrgId, persistedTicket.OrganizationId);
    }
}
