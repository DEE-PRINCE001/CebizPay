using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.IntegrationTests.Recruitment;

public sealed class RecruitmentIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public RecruitmentIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ApplicationDbContext> CreateDbContextAsync()
    {
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Fact]
    public async Task Recruitment_FullLifecycleAndTenantIsolation_PostgresPersistenceWorksCorrectly()
    {
        await using var dbContext = await CreateDbContextAsync();

        // 1. Create two isolated organizations
        var org1 = new Organization("Alpha Org", $"alpha_{Guid.NewGuid():N}@test.com", "+2348011111111");
        org1.TransitionStatus(OrganizationStatus.Verified);
        var org2 = new Organization("Beta Org", $"beta_{Guid.NewGuid():N}@test.com", "+2348022222222");
        org2.TransitionStatus(OrganizationStatus.Verified);
        dbContext.Organizations.AddRange(org1, org2);

        // 2. Add workforce structure for Org 1
        var dept1 = new Department(org1.Id, "Engineering", "Engineering dept");
        var role1 = new WorkforceRole(org1.Id, "Backend Engineer", dept1.Id, "Core APIs");
        var level1 = new SalaryLevel(org1.Id, "Senior", 800000m, "NGN");
        dbContext.Departments.Add(dept1);
        dbContext.WorkforceRoles.Add(role1);
        dbContext.SalaryLevels.Add(level1);

        // 3. Create job postings in both orgs
        var jobOrg1 = new JobPosting(
            org1.Id,
            "Senior .NET Engineer",
            "Develop scalable microservices",
            "usr_hr_alpha",
            EmploymentType.FullTime,
            dept1.Id,
            role1.Id,
            level1.Id,
            "Lagos, Nigeria",
            "C#, EF Core, Postgres",
            "APIs & Workers",
            DateTime.UtcNow.AddDays(14));

        var jobOrg2 = new JobPosting(
            org2.Id,
            "Marketing Lead",
            "Lead marketing campaigns",
            "usr_hr_beta",
            EmploymentType.FullTime,
            location: "Abuja, Nigeria");

        dbContext.JobPostings.AddRange(jobOrg1, jobOrg2);
        await dbContext.SaveChangesAsync();

        // 4. Publish job in Org 1
        jobOrg1.Publish(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();

        // 5. Submit candidate applications for job in Org 1
        var app1 = new RecruitmentApplication(
            jobOrg1.Id,
            org1.Id,
            "Candidate One",
            "candidate1@example.com",
            "+2348099887766",
            "usr_cand1",
            "https://cdn.example.com/resumes/c1.pdf",
            "Strong backend background");

        var app2 = new RecruitmentApplication(
            jobOrg1.Id,
            org1.Id,
            "Candidate Two",
            "candidate2@example.com",
            "+2348099887755",
            "usr_cand2",
            "https://cdn.example.com/resumes/c2.pdf",
            "Enthusiastic junior engineer");

        dbContext.RecruitmentApplications.AddRange(app1, app2);
        await dbContext.SaveChangesAsync();

        // 6. Verify Tenant Isolation on Job Postings
        var org1Jobs = await dbContext.JobPostings.Where(j => j.OrganizationId == org1.Id).ToListAsync();
        Assert.Single(org1Jobs);
        Assert.Equal("Senior .NET Engineer", org1Jobs[0].Title);

        var org2Jobs = await dbContext.JobPostings.Where(j => j.OrganizationId == org2.Id).ToListAsync();
        Assert.Single(org2Jobs);
        Assert.Equal("Marketing Lead", org2Jobs[0].Title);

        // 7. Verify Tenant Isolation on Applications
        var org1Apps = await dbContext.RecruitmentApplications.Where(a => a.OrganizationId == org1.Id).ToListAsync();
        Assert.Equal(2, org1Apps.Count);

        var org2Apps = await dbContext.RecruitmentApplications.Where(a => a.OrganizationId == org2.Id).ToListAsync();
        Assert.Empty(org2Apps);

        // 8. Progress Candidate 1: Review -> Shortlist -> Accept
        var now = DateTime.UtcNow;
        app1.Review("usr_hr_alpha", now, "Good CV");
        app1.Shortlist("usr_hr_alpha", now.AddHours(1), "Passed interview");
        app1.Accept("usr_hr_alpha", now.AddDays(1), "Offer accepted");

        // 9. Reject Candidate 2
        app2.Reject("usr_hr_alpha", "Insufficient experience", now.AddHours(2), "Saved for junior roles");

        await dbContext.SaveChangesAsync();

        // 10. Verify persisted statuses in PostgreSQL
        var reloadedApp1 = await dbContext.RecruitmentApplications.FindAsync(app1.Id);
        Assert.NotNull(reloadedApp1);
        Assert.Equal(ApplicationStatus.Accepted, reloadedApp1.Status);
        Assert.Equal("usr_hr_alpha", reloadedApp1.ReviewedByUserId);

        var reloadedApp2 = await dbContext.RecruitmentApplications.FindAsync(app2.Id);
        Assert.NotNull(reloadedApp2);
        Assert.Equal(ApplicationStatus.Rejected, reloadedApp2.Status);
        Assert.Equal("Insufficient experience", reloadedApp2.RejectionReason);

        // 11. Close job posting
        jobOrg1.Close(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();

        var reloadedJob1 = await dbContext.JobPostings.FindAsync(jobOrg1.Id);
        Assert.NotNull(reloadedJob1);
        Assert.Equal(JobPostingStatus.Closed, reloadedJob1.Status);
        Assert.False(reloadedJob1.IsAcceptingApplications(DateTime.UtcNow));
    }
}
