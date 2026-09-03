using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Admin.Audit;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CebizPay.IntegrationTests.Security;

[Collection("Infrastructure")]
public sealed class AuditQueryIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public AuditQueryIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private ApplicationDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_fixture.PostgresContainer.GetConnectionString())
            .Options;
        return new ApplicationDbContext(opts);
    }

    [Fact]
    public async Task GetAuditLogsQuery_PostgreSql_SuperAdmin_ShouldReturnAllLogsPaged()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();

        var superAdminId = $"superadmin_{Guid.NewGuid():N}";
        var adminProfile = new AdminProfile(superAdminId, AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(adminProfile);

        var orgId = Guid.NewGuid();
        var log1 = AuditLog.Create(superAdminId, AuditActions.FeePolicyCreated, AuditResourceTypes.FeePolicy, "pol-1");
        var log2 = AuditLog.Create("user-x", AuditActions.PeerTransferCompleted, AuditResourceTypes.PeerTransfer, "tx-1", organizationId: orgId);

        db.AuditLogs.AddRange(log1, log2);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(superAdminId);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var handler = new GetAuditLogsQueryHandler(db, currentUserService, orgContext);
        var query = new GetAuditLogsQuery(PageNumber: 1, PageSize: 50);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 2);
        Assert.Contains(result.Items, x => x.Action == AuditActions.FeePolicyCreated && x.ActorId == superAdminId);
        Assert.Contains(result.Items, x => x.Action == AuditActions.PeerTransferCompleted && x.OrganizationId == orgId);
    }

    [Fact]
    public async Task GetAuditLogsQuery_PostgreSql_OrganizationUser_ShouldBeRestrictedToOrg()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();

        var org = new Organization("Test Corp", "test@corp.com", "+2348012345678");
        var myOrgId = org.Id;
        var otherOrg = new Organization("Other Corp", "other@corp.com", "+2348012345679");
        var otherOrgId = otherOrg.Id;

        var orgUserId = $"orguser_{Guid.NewGuid():N}";

        var myLog = AuditLog.Create(orgUserId, AuditActions.PeerTransferCompleted, AuditResourceTypes.PeerTransfer, "tx-mine", organizationId: myOrgId);
        var otherLog = AuditLog.Create("other-user", AuditActions.PeerTransferCompleted, AuditResourceTypes.PeerTransfer, "tx-other", organizationId: otherOrgId);

        var membership = new OrganizationMembership(orgUserId, myOrgId, MembershipRoleType.Owner);
        db.Organizations.AddRange(org, otherOrg);
        db.OrganizationMemberships.Add(membership);
        db.AuditLogs.AddRange(myLog, otherLog);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(orgUserId);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        orgContext.CurrentOrganizationId.Returns(myOrgId);
        orgContext.HasAccessToOrganizationAsync(myOrgId, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new GetAuditLogsQueryHandler(db, currentUserService, orgContext);
        var query = new GetAuditLogsQuery(PageNumber: 1, PageSize: 50);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.All(result.Items, x => Assert.Equal(myOrgId, x.OrganizationId));
    }
}
