using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.IntegrationTests.Workforce;

public sealed class WorkforceStructureIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public WorkforceStructureIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private ApplicationDbContext CreateDbContext()
    {
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task WorkforceStructure_PostgresPersistenceAndTenantIsolation_WorksCorrectly()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureCreatedAsync();

        var org1 = new Organization("Org One", $"org1_{Guid.NewGuid():N}@test.com", "+2348000000010");
        var org2 = new Organization("Org Two", $"org2_{Guid.NewGuid():N}@test.com", "+2348000000020");
        dbContext.Organizations.AddRange(org1, org2);

        var dept1 = new Department(org1.Id, "Finance", "Finance Department Org 1");
        var dept2 = new Department(org2.Id, "Finance", "Finance Department Org 2");
        dbContext.Departments.AddRange(dept1, dept2);

        var role1 = new WorkforceRole(org1.Id, "Accountant", dept1.Id, "Org 1 Accountant");
        var role2 = new WorkforceRole(org2.Id, "Accountant", dept2.Id, "Org 2 Accountant");
        dbContext.WorkforceRoles.AddRange(role1, role2);

        var level1 = new SalaryLevel(org1.Id, "Grade 1", 300000m, "NGN");
        var level2 = new SalaryLevel(org2.Id, "Grade 1", 450000m, "NGN");
        dbContext.SalaryLevels.AddRange(level1, level2);

        await dbContext.SaveChangesAsync();

        // Query Org 1 departments
        var org1Depts = await dbContext.Departments.Where(d => d.OrganizationId == org1.Id).ToListAsync();
        Assert.Single(org1Depts);
        Assert.Equal("Finance", org1Depts[0].Name);

        // Query Org 2 departments
        var org2Depts = await dbContext.Departments.Where(d => d.OrganizationId == org2.Id).ToListAsync();
        Assert.Single(org2Depts);
        Assert.Equal("Finance", org2Depts[0].Name);

        // Verify Roles isolated
        var org1Roles = await dbContext.WorkforceRoles.Where(r => r.OrganizationId == org1.Id).ToListAsync();
        Assert.Single(org1Roles);
        Assert.Equal(dept1.Id, org1Roles[0].DepartmentId);

        // Verify Salary Levels isolated
        var org1Levels = await dbContext.SalaryLevels.Where(s => s.OrganizationId == org1.Id).ToListAsync();
        Assert.Single(org1Levels);
        Assert.Equal(300000m, org1Levels[0].BaseAmount);
    }
}
