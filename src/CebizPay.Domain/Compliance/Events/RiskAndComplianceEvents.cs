using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Compliance.Events;

/// <summary>
/// Domain event published when a compliance risk assessment has been completed.
/// </summary>
public sealed record RiskAssessmentCompletedDomainEvent(
    Guid RiskAssessmentId,
    RiskSubjectType SubjectType,
    string SubjectId,
    RiskRating RiskRating,
    CddLevel CddLevel,
    bool EddRequired,
    string RulesetVersion,
    Guid? OrganizationId,
    DateTime EvaluatedAtUtc);

/// <summary>
/// Domain event published when a risk reassessment results in a changed risk level.
/// </summary>
public sealed record RiskAssessmentChangedDomainEvent(
    Guid PreviousAssessmentId,
    Guid NewAssessmentId,
    RiskSubjectType SubjectType,
    string SubjectId,
    RiskRating PreviousRiskRating,
    RiskRating NewRiskRating,
    Guid? OrganizationId,
    DateTime EvaluatedAtUtc);

/// <summary>
/// Domain event published when CDD verification is completed.
/// </summary>
public sealed record CddCompletedDomainEvent(
    Guid CddProfileId,
    RiskSubjectType SubjectType,
    string SubjectId,
    RiskRating RiskRating,
    CddLevel CddLevel,
    int? Tier,
    Guid? OrganizationId,
    DateTime CompletedAtUtc);

/// <summary>
/// Domain event published when Enhanced Due Diligence (EDD) is triggered.
/// </summary>
public sealed record EddRequiredDomainEvent(
    RiskSubjectType SubjectType,
    string SubjectId,
    Guid RiskAssessmentId,
    string TriggerReason,
    Guid? OrganizationId,
    DateTime TriggeredAtUtc);

/// <summary>
/// Domain event published when an EDD case is formally opened.
/// </summary>
public sealed record EddCaseOpenedDomainEvent(
    Guid EddCaseId,
    string CaseNumber,
    RiskSubjectType SubjectType,
    string SubjectId,
    Guid? OrganizationId,
    DateTime OpenedAtUtc);

/// <summary>
/// Domain event published when additional information is requested for an EDD case.
/// </summary>
public sealed record EddInformationRequestedDomainEvent(
    Guid EddCaseId,
    string CaseNumber,
    string RequestedByUserId,
    DateTime RequestedAtUtc);

/// <summary>
/// Domain event published when customer submits documentation for an EDD case.
/// </summary>
public sealed record EddInformationSubmittedDomainEvent(
    Guid EddCaseId,
    string CaseNumber,
    DateTime SubmittedAtUtc);

/// <summary>
/// Domain event published when an EDD case is approved.
/// </summary>
public sealed record EddCaseApprovedDomainEvent(
    Guid EddCaseId,
    string CaseNumber,
    string ApprovedByUserId,
    bool IsSeniorManagement,
    DateTime ApprovedAtUtc);

/// <summary>
/// Domain event published when an EDD case is rejected.
/// </summary>
public sealed record EddCaseRejectedDomainEvent(
    Guid EddCaseId,
    string CaseNumber,
    string RejectedByUserId,
    string Reason,
    DateTime RejectedAtUtc);

/// <summary>
/// Domain event published when a compliance decision changes or is newly formed.
/// </summary>
public sealed record ComplianceDecisionChangedDomainEvent(
    Guid DecisionId,
    RiskSubjectType SubjectType,
    string SubjectId,
    ComplianceDecisionType Decision,
    RiskRating RiskRating,
    bool IsManualOverride,
    Guid? OrganizationId,
    DateTime DecidedAtUtc);

/// <summary>
/// Domain event published when a compliance restriction is placed on an account.
/// </summary>
public sealed record ComplianceRestrictedDomainEvent(
    Guid RestrictionId,
    RiskSubjectType SubjectType,
    string SubjectId,
    ComplianceRestrictionType RestrictionType,
    string Reason,
    Guid? OrganizationId,
    DateTime PlacedAtUtc);

/// <summary>
/// Domain event published when a compliance restriction is released.
/// </summary>
public sealed record ComplianceRestrictionReleasedDomainEvent(
    Guid RestrictionId,
    RiskSubjectType SubjectType,
    string SubjectId,
    string ReleasedByUserId,
    string ReleaseReason,
    Guid? OrganizationId,
    DateTime ReleasedAtUtc);

/// <summary>
/// Domain event published when a transaction is evaluated for compliance eligibility.
/// </summary>
public sealed record TransactionEligibilityEvaluatedDomainEvent(
    string UserId,
    Guid? OrganizationId,
    ComplianceOperationType OperationType,
    decimal Amount,
    Currency Currency,
    EligibilityStatus Status,
    string? RestrictionReason,
    DateTime EvaluatedAtUtc);
