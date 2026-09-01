using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Service computing authoritative compliance decisions and administering tightly permissioned manual overrides.
/// </summary>
public interface IComplianceDecisionService
{
    /// <summary>
    /// Evaluates or updates the authoritative compliance decision based on current CDD, EDD, and risk states.
    /// </summary>
    Task<ComplianceDecisionDto> EvaluateDecisionAsync(
        RiskSubjectType subjectType,
        string subjectId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies an administrative manual override to a compliance decision with mandatory audit trail.
    /// Non-negotiable regulatory safeguards (e.g. active sanctions match) cannot be bypassed.
    /// </summary>
    Task<ComplianceDecisionDto> ApplyManualOverrideAsync(
        RiskSubjectType subjectType,
        string subjectId,
        ComplianceDecisionType newDecision,
        string reason,
        string adminUserId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);
}
