#pragma warning disable CS1591
using System.Diagnostics.Metrics;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Compliance.Events;
using CebizPay.Infrastructure.Compliance.Services;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class CddServiceTests
{
    private readonly RiskMetrics _metrics;

    public CddServiceTests()
    {
        var meterFactoryMock = Substitute.For<IMeterFactory>();
        meterFactoryMock.Create(Arg.Any<MeterOptions>()).Returns(new Meter("TestMeter"));
        _metrics = new RiskMetrics(meterFactoryMock);
    }

    [Fact]
    public void CddProfile_Create_InitializesIndividualProfileWithNotStarted()
    {
        var profile = CddProfile.Create(
            RiskSubjectType.Individual,
            "usr_individual_1",
            organizationId: null,
            tier: 1);

        Assert.Equal(RiskSubjectType.Individual, profile.SubjectType);
        Assert.Equal("usr_individual_1", profile.SubjectId);
        Assert.Equal(CddStatus.NotStarted, profile.Status);
        Assert.Equal(1, profile.Tier);
        Assert.Null(profile.CompletedAtUtc);
    }

    [Fact]
    public void CddProfile_UpdateFromAssessment_TierCalculationAndCompletion()
    {
        var profile = CddProfile.Create(
            RiskSubjectType.Individual,
            "usr_individual_2",
            organizationId: null,
            tier: 1);

        var assessment = RiskAssessment.Create(
            RiskSubjectType.Individual,
            "usr_individual_2",
            RiskRating.Low,
            CddLevel.Standard,
            eddRequired: false,
            "2026.1",
            "Verified Tier 2 customer");

        profile.UpdateFromAssessment(assessment, tier: 2);

        Assert.Equal(CddStatus.Completed, profile.Status);
        Assert.Equal(RiskRating.Low, profile.RiskRating);
        Assert.Equal(2, profile.Tier);
        Assert.NotNull(profile.CompletedAtUtc);
    }

    [Fact]
    public void CddProfile_UpdateFromAssessment_HighRiskTriggersEnhancedRequired()
    {
        var profile = CddProfile.Create(
            RiskSubjectType.Individual,
            "usr_individual_3",
            organizationId: null,
            tier: 1);

        var assessment = RiskAssessment.Create(
            RiskSubjectType.Individual,
            "usr_individual_3",
            RiskRating.High,
            CddLevel.Enhanced,
            eddRequired: true,
            "2026.1",
            "PEP detected, EDD required.");

        profile.UpdateFromAssessment(assessment, tier: 1);

        Assert.Equal(CddStatus.EnhancedRequired, profile.Status);
        Assert.Equal(RiskRating.High, profile.RiskRating);
        Assert.Equal(CddLevel.Enhanced, profile.CddLevel);
    }

    [Fact]
    public void CddProfile_UpdateFromAssessment_OrganizationKyb_TierIsNull()
    {
        var orgId = Guid.NewGuid();
        var profile = CddProfile.Create(
            RiskSubjectType.Organization,
            orgId.ToString(),
            organizationId: orgId);

        var assessment = RiskAssessment.Create(
            RiskSubjectType.Organization,
            orgId.ToString(),
            RiskRating.Low,
            CddLevel.Standard,
            eddRequired: false,
            "2026.1",
            "Corporate CAC verified",
            organizationId: orgId);

        // CBN regulation: Tiered KYC applies to individuals only; legal persons do not have tiers
        profile.UpdateFromAssessment(assessment, tier: null);

        Assert.Equal(CddStatus.Completed, profile.Status);
        Assert.Null(profile.Tier);
    }
}
