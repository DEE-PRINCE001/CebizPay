using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Service managing granular operational and volume compliance restrictions.
/// </summary>
public interface IComplianceRestrictionService
{
    /// <summary>
    /// Places an active compliance restriction on an individual or organization.
    /// </summary>
    Task<ComplianceRestrictionDto> PlaceRestrictionAsync(
        RiskSubjectType subjectType,
        string subjectId,
        ComplianceRestrictionType restrictionType,
        string reason,
        string placedBy,
        decimal? dailyCapAmount = null,
        decimal? singleCapAmount = null,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases an active compliance restriction with mandatory justification and audit trail.
    /// </summary>
    Task<ComplianceRestrictionDto> ReleaseRestrictionAsync(
        Guid restrictionId,
        string releaseReason,
        string releasedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active restrictions for a subject.
    /// </summary>
    Task<IReadOnlyList<ComplianceRestrictionDto>> GetActiveRestrictionsAsync(
        RiskSubjectType subjectType,
        string subjectId,
        CancellationToken cancellationToken = default);
}
