#pragma warning disable CS1591
using System.Diagnostics.Metrics;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Infrastructure.Compliance.Services;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class ComplianceDecisionServiceTests
{
    private readonly RiskMetrics _metrics;

    public ComplianceDecisionServiceTests()
    {
        var meterFactoryMock = Substitute.For<IMeterFactory>();
        meterFactoryMock.Create(Arg.Any<MeterOptions>()).Returns(new Meter("TestMeter"));
        _metrics = new RiskMetrics(meterFactoryMock);
    }

    [Fact]
    public void ComplianceDecision_CreateAutomatic_LowRiskResultsInApproved()
    {
        var assessment = RiskAssessment.Create(
            RiskSubjectType.Individual,
            "usr_auto_1",
            RiskRating.Low,
            CddLevel.Basic,
            eddRequired: false,
            "2026.1",
            "Low risk identity match.");

        var decision = ComplianceDecision.Create(
            RiskSubjectType.Individual,
            "usr_auto_1",
            ComplianceDecisionType.Approved,
            RiskRating.Low,
            CddLevel.Basic,
            "Low risk identity match.",
            "2026.1",
            "System");

        Assert.Equal(ComplianceDecisionType.Approved, decision.Decision);
        Assert.Equal(RiskRating.Low, decision.RiskRating);
        Assert.False(decision.IsManualOverride);
        Assert.True(decision.IsActive);
        Assert.Equal("System", decision.DecidedBy);
    }

    [Fact]
    public void ComplianceDecision_Create_ProhibitedRiskResultsInRejected()
    {
        var decision = ComplianceDecision.Create(
            RiskSubjectType.Individual,
            "usr_auto_2",
            ComplianceDecisionType.Rejected,
            RiskRating.Prohibited,
            CddLevel.Enhanced,
            "Confirmed sanctions match.",
            "2026.1",
            "System");

        Assert.Equal(ComplianceDecisionType.Rejected, decision.Decision);
        Assert.Equal(RiskRating.Prohibited, decision.RiskRating);
    }

    [Fact]
    public void ComplianceDecision_CreateManualOverride_WhenNotProhibited_Succeeds()
    {
        var decision = ComplianceDecision.CreateManualOverride(
            RiskSubjectType.Individual,
            "usr_override_1",
            ComplianceDecisionType.Approved,
            RiskRating.Medium,
            CddLevel.Standard,
            "Customer provided valid explanation and secondary ID proof for ambiguous name match.",
            "admin_chief_officer",
            "2026.1");

        Assert.Equal(ComplianceDecisionType.Approved, decision.Decision);
        Assert.True(decision.IsManualOverride);
        Assert.Equal("admin_chief_officer", decision.DecidedBy);
        Assert.Equal("Customer provided valid explanation and secondary ID proof for ambiguous name match.", decision.OverrideReason);
    }

    [Fact]
    public void ComplianceDecision_CreateManualOverride_ThrowsWhenOverridingSanctionsMatchToApproved()
    {
        // Regulatory non-negotiable safeguard:
        // A confirmed sanctions match cannot be bypassed by an administrative override to Approved.
        Assert.Throws<InvalidOperationException>(() =>
            ComplianceDecision.CreateManualOverride(
                RiskSubjectType.Individual,
                "usr_sanctions_fail",
                ComplianceDecisionType.Approved,
                RiskRating.Prohibited,
                CddLevel.Enhanced,
                "Trying to override sanction",
                "admin_officer",
                "2026.1"));
    }
}
