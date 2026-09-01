#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.IntegrationTests.Compliance;

public sealed class RiskAndComplianceIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public RiskAndComplianceIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    private async Task<ApplicationDbContext> CreateDbContextAsync()
    {
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    [Fact]
    public async Task RiskAssessment_WithFactors_PersistsAndQueriesSuccessfullyInPostgres()
    {
        using var dbContext = await CreateDbContextAsync();

        var userId = $"usr_it_{Guid.NewGuid():N}";
        var assessment = RiskAssessment.Create(
            RiskSubjectType.Individual,
            userId,
            RiskRating.Low,
            CddLevel.Basic,
            eddRequired: false,
            "2026.1",
            "Initial low risk profile.");

        var factor1 = RiskFactorResult.Create(assessment.Id, "RULE-SANCTIONS-001", "Sanctions", RiskRating.Low, "Clear", severity: 1);
        var factor2 = RiskFactorResult.Create(assessment.Id, "RULE-PEP-001", "PEP", RiskRating.Low, "No match", severity: 1);
        assessment.AddRiskFactor(factor1);
        assessment.AddRiskFactor(factor2);

        dbContext.RiskAssessments.Add(assessment);
        await dbContext.SaveChangesAsync();

        var retrieved = await dbContext.RiskAssessments
            .Include(a => a.RiskFactors)
            .FirstOrDefaultAsync(a => a.Id == assessment.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(userId, retrieved.SubjectId);
        Assert.Equal(RiskRating.Low, retrieved.RiskRating);
        Assert.Equal(2, retrieved.RiskFactors.Count);
    }

    [Fact]
    public async Task CddProfile_And_ComplianceDecision_PersistsAndQueriesInPostgres()
    {
        using var dbContext = await CreateDbContextAsync();

        var userId = $"usr_it_{Guid.NewGuid():N}";
        var cddProfile = CddProfile.Create(
            RiskSubjectType.Individual,
            userId,
            organizationId: null,
            tier: 2);

        var decision = ComplianceDecision.CreateManualOverride(
            RiskSubjectType.Individual,
            userId,
            ComplianceDecisionType.Approved,
            RiskRating.Low,
            CddLevel.Standard,
            "admin_test_officer",
            "Manual review completed with secondary document verified.",
            "2026.1");

        var restriction = ComplianceRestriction.Create(
            RiskSubjectType.Individual,
            userId,
            ComplianceRestrictionType.CapSingleTransaction,
            "Temporary security cap",
            "admin_test_officer",
            singleCapAmount: 100_000m);

        dbContext.CddProfiles.Add(cddProfile);
        dbContext.ComplianceDecisions.Add(decision);
        dbContext.ComplianceRestrictions.Add(restriction);
        await dbContext.SaveChangesAsync();

        var retrievedProfile = await dbContext.CddProfiles.FirstOrDefaultAsync(c => c.SubjectId == userId);
        var retrievedDecision = await dbContext.ComplianceDecisions.FirstOrDefaultAsync(d => d.SubjectId == userId);
        var retrievedRestriction = await dbContext.ComplianceRestrictions.FirstOrDefaultAsync(r => r.SubjectId == userId);

        Assert.NotNull(retrievedProfile);
        Assert.Equal(2, retrievedProfile.Tier);

        Assert.NotNull(retrievedDecision);
        Assert.True(retrievedDecision.IsManualOverride);

        Assert.NotNull(retrievedRestriction);
        Assert.True(retrievedRestriction.IsActive);
        Assert.Equal(100_000m, retrievedRestriction.SingleCapAmount);
    }

    [Fact]
    public async Task EddCase_LifecyclePersistsCorrectlyInPostgres()
    {
        using var dbContext = await CreateDbContextAsync();

        var userId = $"usr_it_{Guid.NewGuid():N}";
        var riskAssessmentId = Guid.NewGuid();

        var eddCase = EddCase.Create(
            RiskSubjectType.Individual,
            userId,
            riskAssessmentId,
            "PEP Match confirmed in external provider.",
            "Declaration of assets and source of wealth.",
            seniorManagementApprovalRequired: true);

        dbContext.EddCases.Add(eddCase);
        await dbContext.SaveChangesAsync();

        // Customer submits info
        eddCase.SubmitInformation("Submitted bank records and asset declaration form.");
        await dbContext.SaveChangesAsync();

        // Senior Management approves
        eddCase.Approve("Wealth documentation fully verified.", "chief_compliance_officer", isSeniorManagement: true);
        await dbContext.SaveChangesAsync();

        var retrieved = await dbContext.EddCases.FirstOrDefaultAsync(e => e.Id == eddCase.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(EddStatus.Approved, retrieved.Status);
        Assert.Equal(ComplianceDecisionType.Approved, retrieved.Decision);
        Assert.Equal("chief_compliance_officer", retrieved.SeniorManagementApproverId);
    }
}
