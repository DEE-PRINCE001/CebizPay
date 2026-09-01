using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Service managing Customer Due Diligence (CDD) regulatory profile state and evaluations.
/// </summary>
public interface ICddService
{
    /// <summary>
    /// Retrieves or initializes a CDD profile for an individual or organization.
    /// </summary>
    Task<CddProfileDto> GetOrCreateCddProfileAsync(
        RiskSubjectType subjectType,
        string subjectId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a full CDD evaluation, re-evaluating risk and advancing the CDD lifecycle.
    /// </summary>
    Task<CddProfileDto> EvaluateCddAsync(
        RiskSubjectType subjectType,
        string subjectId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);
}
