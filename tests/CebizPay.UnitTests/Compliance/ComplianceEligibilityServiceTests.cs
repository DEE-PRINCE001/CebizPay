#pragma warning disable CS1591, CA1861
using System.Diagnostics.Metrics;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Infrastructure.Compliance.Services;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class ComplianceEligibilityServiceTests
{
    private readonly RiskMetrics _metrics;

    public ComplianceEligibilityServiceTests()
    {
        var meterFactoryMock = Substitute.For<IMeterFactory>();
        meterFactoryMock.Create(Arg.Any<MeterOptions>()).Returns(new Meter("TestMeter"));
        _metrics = new RiskMetrics(meterFactoryMock);
    }

    [Fact]
    public void ComplianceRestriction_Create_InitializesActiveState()
    {
        var restriction = ComplianceRestriction.Create(
            RiskSubjectType.Individual,
            "usr_restrict_1",
            ComplianceRestrictionType.BlockBankTransfer,
            "Unusual payout activity detected",
            "admin_risk_manager");

        Assert.Equal(ComplianceRestrictionType.BlockBankTransfer, restriction.RestrictionType);
        Assert.True(restriction.IsActive);
        Assert.Equal("Unusual payout activity detected", restriction.Reason);
        Assert.Null(restriction.ReleasedAtUtc);
    }

    [Fact]
    public void ComplianceRestriction_Release_DeactivatesAndRecordsReason()
    {
        var restriction = ComplianceRestriction.Create(
            RiskSubjectType.Individual,
            "usr_restrict_2",
            ComplianceRestrictionType.BlockCardFunding,
            "Card fraud alert",
            "admin_risk_manager");

        restriction.Release("Charge dispute settled in merchant favor.", "admin_compliance_lead");

        Assert.False(restriction.IsActive);
        Assert.Equal("admin_compliance_lead", restriction.ReleasedBy);
        Assert.Equal("Charge dispute settled in merchant favor.", restriction.ReleaseReason);
        Assert.NotNull(restriction.ReleasedAtUtc);
    }

    [Fact]
    public void TransactionEligibilityResult_Allowed_CreatesPositiveResult()
    {
        var result = TransactionEligibilityResult.Allowed(500_000m);
        Assert.True(result.IsAllowed);
        Assert.Equal(EligibilityStatus.Allowed, result.Status);
        Assert.Equal(500_000m, result.MaxAllowedAmount);
        Assert.Empty(result.TriggeredRestrictions);
    }

    [Fact]
    public void TransactionEligibilityResult_Restricted_CreatesNegativeResult()
    {
        var result = TransactionEligibilityResult.Restricted("Limit exceeded", new[] { "CBN Tier 1 Limit" }, 50_000m);
        Assert.False(result.IsAllowed);
        Assert.Equal(EligibilityStatus.Restricted, result.Status);
        Assert.Single(result.TriggeredRestrictions);
        Assert.Equal("CBN Tier 1 Limit", result.TriggeredRestrictions[0]);
    }
}
