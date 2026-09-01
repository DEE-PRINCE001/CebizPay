#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Infrastructure.Compliance.Rules;
using CebizPay.Infrastructure.Compliance.Services;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class TransactionLimitPolicyTests
{
    private readonly TransactionLimitPolicyService _policyService = new();

    [Fact]
    public void RegulatoryTier1Ceiling_CannotBeExceededByProductPolicy()
    {
        // Even if product config specifies ₦80,000 for Tier 1, CBN regulatory ceiling (₦50,000) non-overridably bounds it
        var policyWithOverriddenProductCap = new TransactionLimitPolicy
        {
            Version = "2026.TEST.OVERRIDE",
            ConfiguredIndividualTier1SingleCap = 80_000m // Exceeds CBN ₦50,000 ceiling
        };

        _policyService.RegisterPolicy(policyWithOverriddenProductCap);

        var effectiveLimit = _policyService.CalculateEffectiveLimit(
            RiskSubjectType.Individual,
            individualTier: 1,
            ComplianceOperationType.BankTransferPayout);

        Assert.Equal(50_000m, effectiveLimit.EffectiveSingleCap);
        Assert.Equal(LimitConstraintSource.RegulatoryCbnTierLimit, effectiveLimit.BindingConstraintSource);
        Assert.Contains("Statutory CBN Tier 1 non-overridable limit", effectiveLimit.Explanation);
    }

    [Fact]
    public void ProductPolicy_CanConfigureTighterLimitWithinRegulatoryBounds()
    {
        // Business configures a tighter ₦30,000 limit for Tier 1
        var policyWithTighterCap = new TransactionLimitPolicy
        {
            Version = "2026.TEST.TIGHT",
            ConfiguredIndividualTier1SingleCap = 30_000m
        };

        _policyService.RegisterPolicy(policyWithTighterCap);

        var effectiveLimit = _policyService.CalculateEffectiveLimit(
            RiskSubjectType.Individual,
            individualTier: 1,
            ComplianceOperationType.BankTransferPayout);

        Assert.Equal(30_000m, effectiveLimit.EffectiveSingleCap);
        Assert.Equal(LimitConstraintSource.CebizPayProductPolicy, effectiveLimit.BindingConstraintSource);
        Assert.Contains("CebizPay policy limit", effectiveLimit.Explanation);
    }

    [Fact]
    public void ProviderRailConstraint_ActsAsExternalCeiling()
    {
        var defaultPolicy = new TransactionLimitPolicy();
        _policyService.RegisterPolicy(defaultPolicy);

        // Tier 3 individual has unrestricted statutory limit, but Flutterwave card funding is capped at ₦2,000,000
        var effectiveLimit = _policyService.CalculateEffectiveLimit(
            RiskSubjectType.Individual,
            individualTier: 3,
            ComplianceOperationType.CardFunding,
            provider: "flutterwave");

        Assert.Equal(2_000_000m, effectiveLimit.EffectiveSingleCap);
        Assert.Equal(LimitConstraintSource.PaymentProviderRailConstraint, effectiveLimit.BindingConstraintSource);
        Assert.Contains("Payment provider (flutterwave) rail constraint", effectiveLimit.Explanation);
    }

    [Fact]
    public void CustomerSpecificRiskRestriction_ClampsLowerThanProductAndRegulatoryLimits()
    {
        var defaultPolicy = new TransactionLimitPolicy();
        _policyService.RegisterPolicy(defaultPolicy);

        // Tier 2 individual (CBN cap ₦200,000) has an active risk restriction single cap of ₦15,000
        var effectiveLimit = _policyService.CalculateEffectiveLimit(
            RiskSubjectType.Individual,
            individualTier: 2,
            ComplianceOperationType.BankTransferPayout,
            customerSingleCap: 15_000m);

        Assert.Equal(15_000m, effectiveLimit.EffectiveSingleCap);
        Assert.Equal(LimitConstraintSource.CustomerRiskRestriction, effectiveLimit.BindingConstraintSource);
        Assert.Contains("Custom account risk restriction single cap", effectiveLimit.Explanation);
    }

    [Fact]
    public void OrganizationLegalPerson_IsStrictlyExemptFromIndividualTieredKycCaps()
    {
        var defaultPolicy = new TransactionLimitPolicy();
        _policyService.RegisterPolicy(defaultPolicy);

        // Organization payout of ₦15,000,000 conforms to corporate product cap (₦25,000,000)
        var effectiveLimit = _policyService.CalculateEffectiveLimit(
            RiskSubjectType.Organization,
            individualTier: null,
            ComplianceOperationType.BankTransferPayout);

        Assert.Null(effectiveLimit.RegulatorySingleCap); // Regulatory tiered KYC does not apply to legal persons
        Assert.Equal(25_000_000m, effectiveLimit.EffectiveSingleCap);
    }

    [Fact]
    public async Task ChangingPolicyVersion_DoesNotAlterHistoricalRiskAssessmentOrDecision()
    {
        // 1. Create a historical assessment under Policy Version 2026.1
        var policyV1 = new TransactionLimitPolicy
        {
            Version = "2026.1",
            IndividualEddVolumeThreshold = 5_000_000m
        };
        _policyService.RegisterPolicy(policyV1);

        var rule = new TransactionProfileVolumeRule(_policyService);

        var contextV1 = new RiskEvaluationContext
        {
            SubjectType = RiskSubjectType.Transaction,
            SubjectId = "txn_historical_1",
            TransactionAmount = 6_000_000m
        };

        var ruleResultV1 = await rule.EvaluateAsync(contextV1);
        Assert.Equal(RiskRating.High, ruleResultV1.RiskRating);
        Assert.True(ruleResultV1.TriggersEdd);

        var historicalAssessment = RiskAssessment.Create(
            RiskSubjectType.Transaction,
            "txn_historical_1",
            ruleResultV1.RiskRating,
            CddLevel.Enhanced,
            eddRequired: true,
            rulesetVersion: rule.RulesetVersion,
            summary: ruleResultV1.Reason);

        var historicalFactor = RiskFactorResult.Create(
            historicalAssessment.Id,
            rule.RuleId,
            rule.RuleName,
            ruleResultV1.RiskRating,
            ruleResultV1.Reason,
            severity: 3);
        historicalAssessment.AddRiskFactor(historicalFactor);

        var historicalDecision = ComplianceDecision.Create(
            RiskSubjectType.Transaction,
            "txn_historical_1",
            ComplianceDecisionType.EddRequired,
            ruleResultV1.RiskRating,
            CddLevel.Enhanced,
            ruleResultV1.Reason,
            historicalAssessment.RulesetVersion,
            decidedBy: "System");

        // 2. Now update policy to Version 2026.2 with higher EDD volume threshold (₦10,000,000)
        var policyV2 = new TransactionLimitPolicy
        {
            Version = "2026.2",
            IndividualEddVolumeThreshold = 10_000_000m
        };
        _policyService.RegisterPolicy(policyV2);

        // 3. Verify that new evaluation under V2 produces a different result for ₦6,000,000
        var ruleResultV2 = await rule.EvaluateAsync(contextV1);
        Assert.Equal(RiskRating.Medium, ruleResultV2.RiskRating);
        Assert.False(ruleResultV2.TriggersEdd);

        // 4. Verify that historical records remain completely unchanged and immutable
        Assert.Equal("2026.1", historicalAssessment.RulesetVersion);
        Assert.Equal(RiskRating.High, historicalAssessment.RiskRating);
        Assert.True(historicalAssessment.EddRequired);
        Assert.Single(historicalAssessment.RiskFactors);
        Assert.Contains("Policy 2026.1", historicalAssessment.RiskFactors.First().Reason);

        Assert.Equal("2026.1", historicalDecision.RulesetVersion);
        Assert.Equal(ComplianceDecisionType.EddRequired, historicalDecision.Decision);
        Assert.Equal(RiskRating.High, historicalDecision.RiskRating);
    }
}
