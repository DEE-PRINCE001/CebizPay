#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Execution context provided to risk rules for comprehensive risk assessment.
/// </summary>
public sealed class RiskEvaluationContext
{
    public RiskSubjectType SubjectType { get; init; }
    public string SubjectId { get; init; } = string.Empty;
    public Guid? OrganizationId { get; init; }
    public IndividualProfile? IndividualProfile { get; init; }
    public Organization? Organization { get; init; }
    public KybDetail? KybDetail { get; init; }
    public IReadOnlyList<KycDocument> KycDocuments { get; init; } = Array.Empty<KycDocument>();
    public IReadOnlyList<VerificationEvidence> VerificationEvidences { get; init; } = Array.Empty<VerificationEvidence>();
    public ComplianceOperationType? OperationType { get; init; }
    public decimal? TransactionAmount { get; init; }
    public Currency? Currency { get; init; }
}

/// <summary>
/// Result produced by an individual deterministic risk rule evaluation.
/// </summary>
public sealed record RiskRuleEvaluationResult(
    string RuleId,
    string RuleName,
    RiskRating RiskRating,
    string Reason,
    string? EvidenceReference = null,
    int Severity = 1,
    bool TriggersEdd = false,
    bool RequiresSeniorManagement = false,
    bool BlocksImmediately = false)
{
    public static RiskRuleEvaluationResult Low(string ruleId, string ruleName, string reason, string? evidenceRef = null) =>
        new(ruleId, ruleName, RiskRating.Low, reason, evidenceRef, 1);

    public static RiskRuleEvaluationResult Medium(string ruleId, string ruleName, string reason, string? evidenceRef = null, int severity = 2) =>
        new(ruleId, ruleName, RiskRating.Medium, reason, evidenceRef, severity);

    public static RiskRuleEvaluationResult High(string ruleId, string ruleName, string reason, string? evidenceRef = null, bool triggersEdd = true, bool requiresSeniorMgmt = false) =>
        new(ruleId, ruleName, RiskRating.High, reason, evidenceRef, 3, TriggersEdd: triggersEdd, RequiresSeniorManagement: requiresSeniorMgmt);

    public static RiskRuleEvaluationResult Prohibited(string ruleId, string ruleName, string reason, string? evidenceRef = null) =>
        new(ruleId, ruleName, RiskRating.Prohibited, reason, evidenceRef, 4, TriggersEdd: false, BlocksImmediately: true);
}

/// <summary>
/// Complete outcome of a comprehensive risk assessment.
/// </summary>
public sealed record RiskAssessmentResult(
    Guid RiskAssessmentId,
    RiskSubjectType SubjectType,
    string SubjectId,
    Guid? OrganizationId,
    RiskRating RiskRating,
    CddLevel CddLevel,
    bool EddRequired,
    bool SeniorManagementApprovalRequired,
    string RulesetVersion,
    DateTime EvaluatedAtUtc,
    DateTime? ExpiresAtUtc,
    string Summary,
    IReadOnlyList<RiskFactorDto> RiskFactors);

/// <summary>
/// Data transfer record for an individual risk factor finding.
/// </summary>
public sealed record RiskFactorDto(
    string RuleId,
    string RuleName,
    RiskRating RiskRating,
    string Reason,
    string? EvidenceReference,
    int Severity);

/// <summary>
/// Data transfer record for a Customer Due Diligence (CDD) profile.
/// </summary>
public sealed record CddProfileDto(
    Guid CddProfileId,
    RiskSubjectType SubjectType,
    string SubjectId,
    Guid? OrganizationId,
    CddStatus Status,
    RiskRating RiskRating,
    CddLevel CddLevel,
    int? Tier,
    Guid? LatestRiskAssessmentId,
    DateTime? CompletedAtUtc,
    DateTime LastEvaluatedAtUtc,
    string? ReviewNotes);

/// <summary>
/// Data transfer record for an Enhanced Due Diligence (EDD) case.
/// </summary>
public sealed record EddCaseDto(
    Guid EddCaseId,
    string CaseNumber,
    RiskSubjectType SubjectType,
    string SubjectId,
    Guid? OrganizationId,
    Guid RiskAssessmentId,
    EddStatus Status,
    string TriggerReason,
    string RequiredInformation,
    string? SubmittedInformation,
    string? AssignedReviewerId,
    string? ReviewedByUserId,
    bool SeniorManagementApprovalRequired,
    string? SeniorManagementApproverId,
    ComplianceDecisionType? Decision,
    string? DecisionReason,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? CompletedAtUtc);

/// <summary>
/// Data transfer record for an authoritative compliance decision.
/// </summary>
public sealed record ComplianceDecisionDto(
    Guid DecisionId,
    RiskSubjectType SubjectType,
    string SubjectId,
    Guid? OrganizationId,
    ComplianceDecisionType Decision,
    RiskRating RiskRating,
    CddLevel CddLevel,
    EddStatus? EddStatus,
    string DecisionReasons,
    string RulesetVersion,
    string DecidedBy,
    bool IsManualOverride,
    string? OverrideReason,
    DateTime EffectiveFromUtc,
    DateTime? ExpiresAtUtc,
    bool IsActive);

/// <summary>
/// Data transfer record for a compliance restriction.
/// </summary>
public sealed record ComplianceRestrictionDto(
    Guid RestrictionId,
    RiskSubjectType SubjectType,
    string SubjectId,
    Guid? OrganizationId,
    ComplianceRestrictionType RestrictionType,
    string Reason,
    decimal? DailyCapAmount,
    decimal? SingleCapAmount,
    string PlacedBy,
    DateTime PlacedAtUtc,
    bool IsActive,
    string? ReleasedBy,
    DateTime? ReleasedAtUtc,
    string? ReleaseReason);

/// <summary>
/// Outcome of evaluating transaction compliance eligibility before financial processing.
/// </summary>
public sealed record TransactionEligibilityResult(
    bool IsAllowed,
    EligibilityStatus Status,
    string? RestrictionReason,
    decimal? MaxAllowedAmount,
    IReadOnlyList<string> TriggeredRestrictions)
{
    private static readonly string[] ReviewRequiredRestrictions = new[] { "Compliance review required before transaction execution." };
    private static readonly string[] EddRequiredRestrictions = new[] { "Enhanced Due Diligence (EDD) must be completed before transaction execution." };
    private static readonly string[] SuspendedRestrictions = new[] { "Account is suspended by compliance." };

    public static TransactionEligibilityResult Allowed(decimal? maxAllowedAmount = null) =>
        new(true, EligibilityStatus.Allowed, null, maxAllowedAmount, Array.Empty<string>());

    public static TransactionEligibilityResult Restricted(string reason, IReadOnlyList<string> triggeredRestrictions, decimal? maxAllowedAmount = null) =>
        new(false, EligibilityStatus.Restricted, reason, maxAllowedAmount, triggeredRestrictions);

    public static TransactionEligibilityResult ReviewRequired(string reason) =>
        new(false, EligibilityStatus.ReviewRequired, reason, null, ReviewRequiredRestrictions);

    public static TransactionEligibilityResult EddRequired(string reason) =>
        new(false, EligibilityStatus.EddRequired, reason, null, EddRequiredRestrictions);

    public static TransactionEligibilityResult Suspended(string reason) =>
        new(false, EligibilityStatus.Suspended, reason, null, SuspendedRestrictions);
}
