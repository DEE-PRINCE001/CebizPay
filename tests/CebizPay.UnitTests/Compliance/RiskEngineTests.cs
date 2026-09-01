#pragma warning disable CS1591
using System.Diagnostics.Metrics;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Infrastructure.Compliance.Rules;
using CebizPay.Infrastructure.Compliance.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class RiskEngineTests
{
    private readonly IApplicationDbContext _dbContextMock;
    private readonly IOutboxService _outboxMock;
    private readonly RiskMetrics _metrics;
    private readonly List<IRiskRule> _rules;

    public RiskEngineTests()
    {
        _dbContextMock = Substitute.For<IApplicationDbContext>();
        _outboxMock = Substitute.For<IOutboxService>();

        var meterFactoryMock = Substitute.For<IMeterFactory>();
        meterFactoryMock.Create(Arg.Any<MeterOptions>()).Returns(new Meter("TestMeter"));
        _metrics = new RiskMetrics(meterFactoryMock);

        _rules = new List<IRiskRule>
        {
            new SanctionsScreeningRule(),
            new PepScreeningRule(),
            new AdverseMediaScreeningRule(),
            new IdentityVerificationRule(),
            new CacCorporateRegistryRule(),
            new BeneficialOwnershipRule(),
            new BiometricLivenessRule(),
            new TransactionProfileVolumeRule()
        };
    }

    [Fact]
    public async Task SanctionsScreeningRule_Match_ReturnsProhibitedRating()
    {
        var rule = new SanctionsScreeningRule();
        var evidence = VerificationEvidence.Create(
            Guid.NewGuid(),
            VerificationType.IndividualKyc,
            VerificationCapability.AmlScreening,
            VerificationProvider.Dojah,
            VerificationResultStatus.Match,
            userId: "user_sanctions_1",
            safeMetadata: "{\"sanction_match\": true, \"list\": \"OFAC\"}");

        var context = new RiskEvaluationContext
        {
            SubjectType = RiskSubjectType.Individual,
            SubjectId = "user_sanctions_1",
            VerificationEvidences = new[] { evidence }
        };

        var result = await rule.EvaluateAsync(context);

        Assert.Equal(RiskRating.Prohibited, result.RiskRating);
        Assert.Equal("RULE-SANCTIONS-001", result.RuleId);
        Assert.Contains("sanctions", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PepScreeningRule_Match_ReturnsHighRiskAndTriggersEdd()
    {
        var rule = new PepScreeningRule();
        var evidence = VerificationEvidence.Create(
            Guid.NewGuid(),
            VerificationType.IndividualKyc,
            VerificationCapability.AmlScreening,
            VerificationProvider.SmileId,
            VerificationResultStatus.Match,
            userId: "user_pep_1",
            safeMetadata: "{\"pep\": true, \"pep_tier\": 1}");

        var context = new RiskEvaluationContext
        {
            SubjectType = RiskSubjectType.Individual,
            SubjectId = "user_pep_1",
            VerificationEvidences = new[] { evidence }
        };

        var result = await rule.EvaluateAsync(context);

        Assert.Equal(RiskRating.High, result.RiskRating);
        Assert.True(result.TriggersEdd);
        Assert.True(result.RequiresSeniorManagement);
    }

    [Fact]
    public async Task CacCorporateRegistryRule_Match_ReturnsLowRiskForOrganization()
    {
        var rule = new CacCorporateRegistryRule();
        var evidence = VerificationEvidence.Create(
            Guid.NewGuid(),
            VerificationType.OrganizationKyb,
            VerificationCapability.Business,
            VerificationProvider.Dojah,
            VerificationResultStatus.Match,
            organizationId: Guid.NewGuid(),
            safeMetadata: "{\"rc_number\": \"RC123456\", \"status\": \"ACTIVE\"}");

        var context = new RiskEvaluationContext
        {
            SubjectType = RiskSubjectType.Organization,
            SubjectId = Guid.NewGuid().ToString(),
            VerificationEvidences = new[] { evidence }
        };

        var result = await rule.EvaluateAsync(context);

        Assert.Equal(RiskRating.Low, result.RiskRating);
        Assert.False(result.TriggersEdd);
    }

    [Fact]
    public async Task IdentityVerificationRule_Mismatch_ReturnsHighRisk()
    {
        var rule = new IdentityVerificationRule();
        var evidence = VerificationEvidence.Create(
            Guid.NewGuid(),
            VerificationType.IndividualKyc,
            VerificationCapability.Identity,
            VerificationProvider.Dojah,
            VerificationResultStatus.Mismatch,
            userId: "user_mismatch_1",
            safeMetadata: "{\"mismatch_fields\": [\"DateOfBirth\"]}");

        var context = new RiskEvaluationContext
        {
            SubjectType = RiskSubjectType.Individual,
            SubjectId = "user_mismatch_1",
            VerificationEvidences = new[] { evidence }
        };

        var result = await rule.EvaluateAsync(context);

        Assert.Equal(RiskRating.High, result.RiskRating);
        Assert.Contains("discrepancy", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransactionProfileVolumeRule_HighVolume_ReturnsHighRiskAndTriggersEdd()
    {
        var rule = new TransactionProfileVolumeRule();
        var context = new RiskEvaluationContext
        {
            SubjectType = RiskSubjectType.Transaction,
            SubjectId = "txn_123",
            OperationType = ComplianceOperationType.BankTransferPayout,
            TransactionAmount = 10_000_000m,
            Currency = Currency.NGN
        };

        var result = await rule.EvaluateAsync(context);

        Assert.Equal(RiskRating.High, result.RiskRating);
        Assert.True(result.TriggersEdd);
        Assert.Contains("₦10,000,000.00", result.Reason);
    }

    [Fact]
    public void RiskAssessment_Create_InitializesProperlyWithExplainableFactors()
    {
        var assessment = RiskAssessment.Create(
            RiskSubjectType.Individual,
            "usr_test_100",
            RiskRating.Low,
            CddLevel.Basic,
            eddRequired: false,
            "2026.1",
            "Low risk customer.");

        var factor1 = RiskFactorResult.Create(assessment.Id, "RULE-SANCTIONS-001", "Sanctions", RiskRating.Low, "Clean", severity: 1);
        var factor2 = RiskFactorResult.Create(assessment.Id, "RULE-ID-001", "Identity", RiskRating.Low, "Verified BVN", severity: 1);
        assessment.AddRiskFactor(factor1);
        assessment.AddRiskFactor(factor2);

        Assert.Equal(RiskRating.Low, assessment.RiskRating);
        Assert.Equal(CddLevel.Basic, assessment.CddLevel);
        Assert.False(assessment.EddRequired);
        Assert.True(assessment.IsCurrent);
        Assert.Equal(2, assessment.RiskFactors.Count);
    }

    [Fact]
    public void RiskAssessment_Archive_SetsIsCurrentFalse()
    {
        var assessment = RiskAssessment.Create(
            RiskSubjectType.Individual,
            "usr_test_200",
            RiskRating.Medium,
            CddLevel.Standard,
            eddRequired: false,
            "2026.1",
            "Initial medium risk.");

        Assert.True(assessment.IsCurrent);
        assessment.MarkSuperseded();
        Assert.False(assessment.IsCurrent);
    }
}
