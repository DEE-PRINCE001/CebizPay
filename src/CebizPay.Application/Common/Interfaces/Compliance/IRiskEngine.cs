using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Centralized Risk Engine evaluating deterministic, explainable risk ratings and CDD requirements.
/// </summary>
public interface IRiskEngine
{
    /// <summary>
    /// Evaluates or reassesses the compliance risk rating for an individual natural person.
    /// </summary>
    Task<RiskAssessmentResult> EvaluateIndividualRiskAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates or reassesses the compliance risk rating for a corporate organization (legal person).
    /// </summary>
    Task<RiskAssessmentResult> EvaluateOrganizationRiskAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates transaction-specific risk for individual high-value, cross-border, or unusual operations.
    /// </summary>
    Task<RiskAssessmentResult> EvaluateTransactionRiskAsync(
        string userId,
        Guid? organizationId,
        ComplianceOperationType operationType,
        decimal amount,
        Currency currency,
        CancellationToken cancellationToken = default);
}
