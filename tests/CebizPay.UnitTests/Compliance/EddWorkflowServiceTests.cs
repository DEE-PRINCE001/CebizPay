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

public sealed class EddWorkflowServiceTests
{
    private readonly RiskMetrics _metrics;

    public EddWorkflowServiceTests()
    {
        var meterFactoryMock = Substitute.For<IMeterFactory>();
        meterFactoryMock.Create(Arg.Any<MeterOptions>()).Returns(new Meter("TestMeter"));
        _metrics = new RiskMetrics(meterFactoryMock);
    }

    [Fact]
    public void EddCase_Create_InitializesInRequiredStatus()
    {
        var riskAssessmentId = Guid.NewGuid();
        var eddCase = EddCase.Create(
            RiskSubjectType.Individual,
            "usr_pep_10",
            riskAssessmentId,
            "Identified as Politically Exposed Person (PEP).",
            "Proof of source of funds, PEP declaration form.",
            seniorManagementApprovalRequired: true);

        Assert.Equal(EddStatus.Required, eddCase.Status);
        Assert.NotNull(eddCase.CaseNumber);
        Assert.StartsWith("EDD-", eddCase.CaseNumber);
        Assert.True(eddCase.SeniorManagementApprovalRequired);
        Assert.Null(eddCase.CompletedAtUtc);
    }

    [Fact]
    public void EddCase_RequestInformation_TransitionsToInformationRequested()
    {
        var eddCase = EddCase.Create(
            RiskSubjectType.Individual,
            "usr_test_11",
            Guid.NewGuid(),
            "High volume threshold triggered",
            "Bank statements for last 6 months",
            seniorManagementApprovalRequired: false);

        eddCase.RequestInformation("Please also upload audited financial report.", "admin_officer_1");

        Assert.Equal(EddStatus.InformationRequested, eddCase.Status);
        Assert.Contains("audited financial report", eddCase.RequiredInformation);
    }

    [Fact]
    public void EddCase_SubmitInformation_TransitionsToInformationSubmitted()
    {
        var eddCase = EddCase.Create(
            RiskSubjectType.Individual,
            "usr_test_12",
            Guid.NewGuid(),
            "PEP exposure",
            "Proof of wealth",
            seniorManagementApprovalRequired: true);

        eddCase.SubmitInformation("Uploaded 2025 tax clearance and bank statement.");

        Assert.Equal(EddStatus.InformationSubmitted, eddCase.Status);
        Assert.Equal("Uploaded 2025 tax clearance and bank statement.", eddCase.SubmittedInformation);
    }

    [Fact]
    public void EddCase_Approve_WhenSeniorManagementRequired_ThrowsIfNotSeniorManagement()
    {
        var eddCase = EddCase.Create(
            RiskSubjectType.Individual,
            "usr_pep_14",
            Guid.NewGuid(),
            "PEP Match",
            "Source of funds",
            seniorManagementApprovalRequired: true);

        eddCase.SubmitInformation("Submitted full PEP disclosure form.");

        // Regular compliance officer cannot sign off on Senior Management PEP approval
        Assert.Throws<InvalidOperationException>(() =>
            eddCase.Approve("Looks good", "compliance_officer_junior", isSeniorManagement: false));
    }

    [Fact]
    public void EddCase_Approve_WhenSeniorManagementApproves_Succeeds()
    {
        var eddCase = EddCase.Create(
            RiskSubjectType.Individual,
            "usr_pep_15",
            Guid.NewGuid(),
            "PEP Match",
            "Source of funds",
            seniorManagementApprovalRequired: true);

        eddCase.SubmitInformation("Submitted full PEP disclosure form.");
        eddCase.Approve("Source of wealth and funds verified by executive team.", "chief_compliance_officer", isSeniorManagement: true);

        Assert.Equal(EddStatus.Approved, eddCase.Status);
        Assert.Equal(ComplianceDecisionType.Approved, eddCase.Decision);
        Assert.Equal("chief_compliance_officer", eddCase.SeniorManagementApproverId);
        Assert.NotNull(eddCase.CompletedAtUtc);
    }

    [Fact]
    public void EddCase_Reject_SetsStatusRejectedAndRecordsReason()
    {
        var eddCase = EddCase.Create(
            RiskSubjectType.Individual,
            "usr_test_16",
            Guid.NewGuid(),
            "Adverse media flags",
            "Explanation of regulatory proceedings",
            seniorManagementApprovalRequired: false);

        eddCase.Reject("Unable to provide legitimate documentation for source of wealth.", "admin_officer_2");

        Assert.Equal(EddStatus.Rejected, eddCase.Status);
        Assert.Equal(ComplianceDecisionType.Rejected, eddCase.Decision);
        Assert.Equal("Unable to provide legitimate documentation for source of wealth.", eddCase.DecisionReason);
        Assert.NotNull(eddCase.CompletedAtUtc);
    }
}
