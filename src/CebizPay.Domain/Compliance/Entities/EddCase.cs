using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Domain.Compliance.Entities;

/// <summary>
/// Domain entity managing the lifecycle of an Enhanced Due Diligence (EDD) case.
/// Enforces formal evidence collection, reviewer assignment, senior management approval boundaries, and audit trail.
/// </summary>
public class EddCase
{
    /// <summary>Unique case identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Canonical human-readable case reference (e.g. EDD-20260901-...).</summary>
    public string CaseNumber { get; private set; } = string.Empty;

    /// <summary>Subject category (Individual or Organization).</summary>
    public RiskSubjectType SubjectType { get; private set; }

    /// <summary>Subject identifier (UserId or OrganizationId string).</summary>
    public string SubjectId { get; private set; } = string.Empty;

    /// <summary>Optional organization identifier for multi-tenant isolation.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Associated RiskAssessment identifier that triggered this EDD case.</summary>
    public Guid RiskAssessmentId { get; private set; }

    /// <summary>Current EDD lifecycle status.</summary>
    public EddStatus Status { get; private set; } = EddStatus.Required;

    /// <summary>Explanation of what triggered the mandatory EDD workflow (e.g. PEP, high transaction volume, complex ownership).</summary>
    public string TriggerReason { get; private set; } = string.Empty;

    /// <summary>Description of required documentation (e.g. Source of Funds, Source of Wealth, Bank Statements, Proof of Business Purpose).</summary>
    public string RequiredInformation { get; private set; } = string.Empty;

    /// <summary>Summary or reference notes of information submitted by the customer.</summary>
    public string? SubmittedInformation { get; private set; }

    /// <summary>Admin user ID assigned as primary case investigator.</summary>
    public string? AssignedReviewerId { get; private set; }

    /// <summary>Admin user ID who made the final compliance decision.</summary>
    public string? ReviewedByUserId { get; private set; }

    /// <summary>Indicates whether CBN regulation or policy mandates Senior Management approval (e.g. for PEPs).</summary>
    public bool SeniorManagementApprovalRequired { get; private set; }

    /// <summary>Senior management user ID who approved the case if required.</summary>
    public string? SeniorManagementApproverId { get; private set; }

    /// <summary>Final compliance outcome decision.</summary>
    public ComplianceDecisionType? Decision { get; private set; }

    /// <summary>Detailed justification for the final decision.</summary>
    public string? DecisionReason { get; private set; }

    /// <summary>Case creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Case last update timestamp.</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Case completion timestamp.</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    private EddCase() { } // EF Core

    /// <summary>
    /// Creates a newly triggered EDD case.
    /// </summary>
    public static EddCase Create(
        RiskSubjectType subjectType,
        string subjectId,
        Guid riskAssessmentId,
        string triggerReason,
        string requiredInformation,
        bool seniorManagementApprovalRequired = false,
        Guid? organizationId = null)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("SubjectId is required.", nameof(subjectId));
        if (riskAssessmentId == Guid.Empty)
            throw new ArgumentException("RiskAssessmentId is required.", nameof(riskAssessmentId));
        if (string.IsNullOrWhiteSpace(triggerReason))
            throw new ArgumentException("TriggerReason is required.", nameof(triggerReason));
        if (string.IsNullOrWhiteSpace(requiredInformation))
            throw new ArgumentException("RequiredInformation is required.", nameof(requiredInformation));

        var now = DateTime.UtcNow;
        var caseNumber = $"EDD-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();

        return new EddCase
        {
            Id = Guid.NewGuid(),
            CaseNumber = caseNumber,
            SubjectType = subjectType,
            SubjectId = subjectId.Trim(),
            OrganizationId = organizationId,
            RiskAssessmentId = riskAssessmentId,
            Status = EddStatus.Required,
            TriggerReason = triggerReason.Trim(),
            RequiredInformation = requiredInformation.Trim(),
            SeniorManagementApprovalRequired = seniorManagementApprovalRequired,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    /// <summary>
    /// Formally requests additional documentation from the customer.
    /// </summary>
    public void RequestInformation(string additionalRequirement, string adminUserId)
    {
        if (Status == EddStatus.Approved || Status == EddStatus.Rejected)
            throw new InvalidOperationException($"Cannot request information on terminal EDD case status {Status}.");

        RequiredInformation = string.IsNullOrWhiteSpace(additionalRequirement)
            ? RequiredInformation
            : $"{RequiredInformation}\n[Update]: {additionalRequirement.Trim()}";

        Status = EddStatus.InformationRequested;
        ReviewedByUserId = adminUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Records information submitted by the customer.
    /// </summary>
    public void SubmitInformation(string submittedInformation)
    {
        if (Status == EddStatus.Approved || Status == EddStatus.Rejected)
            throw new InvalidOperationException($"Cannot submit information on terminal EDD case status {Status}.");

        if (string.IsNullOrWhiteSpace(submittedInformation))
            throw new ArgumentException("Submitted information cannot be empty.", nameof(submittedInformation));

        SubmittedInformation = submittedInformation.Trim();
        Status = EddStatus.InformationSubmitted;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Assigns a compliance officer as case investigator.
    /// </summary>
    public void AssignReviewer(string reviewerAdminUserId)
    {
        if (Status == EddStatus.Approved || Status == EddStatus.Rejected)
            throw new InvalidOperationException($"Cannot reassign reviewer on terminal EDD case status {Status}.");

        AssignedReviewerId = reviewerAdminUserId.Trim();
        if (Status == EddStatus.Required || Status == EddStatus.InformationSubmitted)
        {
            Status = EddStatus.InReview;
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Approves the EDD case, verifying senior management authorization if required.
    /// </summary>
    public void Approve(string reason, string adminUserId, bool isSeniorManagement = false)
    {
        if (Status == EddStatus.Approved || Status == EddStatus.Rejected)
            throw new InvalidOperationException($"Cannot approve terminal EDD case status {Status}.");

        if (SeniorManagementApprovalRequired && !isSeniorManagement)
            throw new InvalidOperationException("Senior management approval is required for this EDD case.");

        var now = DateTime.UtcNow;
        Status = EddStatus.Approved;
        Decision = ComplianceDecisionType.Approved;
        DecisionReason = string.IsNullOrWhiteSpace(reason) ? "EDD requirements satisfied." : reason.Trim();
        ReviewedByUserId = adminUserId;
        if (isSeniorManagement)
            SeniorManagementApproverId = adminUserId;

        CompletedAtUtc = now;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Rejects the EDD case due to inadequate evidence or prohibitive risk.
    /// </summary>
    public void Reject(string reason, string adminUserId)
    {
        if (Status == EddStatus.Approved || Status == EddStatus.Rejected)
            throw new InvalidOperationException($"Cannot reject terminal EDD case status {Status}.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required.", nameof(reason));

        var now = DateTime.UtcNow;
        Status = EddStatus.Rejected;
        Decision = ComplianceDecisionType.Rejected;
        DecisionReason = reason.Trim();
        ReviewedByUserId = adminUserId;
        CompletedAtUtc = now;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Escalates the EDD case for executive/board level disposition.
    /// </summary>
    public void Escalate(string reason, string adminUserId)
    {
        if (Status == EddStatus.Approved || Status == EddStatus.Rejected)
            throw new InvalidOperationException($"Cannot escalate terminal EDD case status {Status}.");

        Status = EddStatus.Escalated;
        DecisionReason = reason.Trim();
        ReviewedByUserId = adminUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
