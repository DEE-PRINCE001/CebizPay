using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CebizPay.IntegrationTests;

public sealed class TenantIsolationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public TenantIsolationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TenantIsolation_UserInOrgA_AccessingOrgBResource_ShouldBeRejected()
    {
        // Arrange database
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var userAId = $"user_org_a_{Guid.NewGuid():N}";
        var orgA = new Organization("Org Alpha", $"alpha_{Guid.NewGuid():N}@test.com", "+2348000000001");
        var orgB = new Organization("Org Beta", $"beta_{Guid.NewGuid():N}@test.com", "+2348000000002");

        var membershipA = new OrganizationMembership(userAId, orgA.Id, MembershipRoleType.Owner);

        dbContext.Organizations.AddRange(orgA, orgB);
        dbContext.OrganizationMemberships.Add(membershipA);
        await dbContext.SaveChangesAsync();

        // Setup HttpContext for User A
        var httpContext = new DefaultHttpContext();
        var claims = new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userAId) };
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, "TestAuth"));

        var httpAccessor = Substitute.For<IHttpContextAccessor>();
        httpAccessor.HttpContext.Returns(httpContext);

        var tenantContext = new CurrentOrganizationContext(httpAccessor, dbContext);

        // Act & Assert
        // User A accessing Org A -> Allowed
        var hasAccessToOrgA = await tenantContext.HasAccessToOrganizationAsync(orgA.Id);
        Assert.True(hasAccessToOrgA, "User A should have access to Organization A.");

        // User A accessing Org B -> REJECTED (Tenant Isolation)
        var hasAccessToOrgB = await tenantContext.HasAccessToOrganizationAsync(orgB.Id);
        Assert.False(hasAccessToOrgB, "User A must NOT be allowed access to Organization B resource.");
    }
}
