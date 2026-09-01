using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Domain.Compliance.Entities;

/// <summary>
/// Domain entity recording an active or historical compliance restriction applied to a customer's account or organization.
/// Enforces fail-closed transaction gating before financial processing without directly mutating the ledger.
/// </summary>
public class ComplianceRestriction
{
    /// <summary>Unique restriction identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Subject category (Individual or Organization).</summary>
    public RiskSubjectType SubjectType { get; private set; }

    /// <summary>Subject identifier (UserId or OrganizationId string).</summary>
    public string SubjectId { get; private set; } = string.Empty;

    /// <summary>Optional organization identifier for multi-tenant isolation.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Type of operational or financial restriction.</summary>
    public ComplianceRestrictionType RestrictionType { get; private set; }

    /// <summary>Justification for placing the restriction.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Optional daily volume cap amount enforced by this restriction.</summary>
    public decimal? DailyCapAmount { get; private set; }

    /// <summary>Optional single transaction cap amount enforced by this restriction.</summary>
    public decimal? SingleCapAmount { get; private set; }

    /// <summary>Actor that placed the restriction ("System" or Admin UserId).</summary>
    public string PlacedBy { get; private set; } = "System";

    /// <summary>Timestamp when the restriction was placed.</summary>
    public DateTime PlacedAtUtc { get; private set; }

    /// <summary>Indicates whether this restriction is currently actively enforced.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Admin user ID who authorized releasing the restriction.</summary>
    public string? ReleasedBy { get; private set; }

    /// <summary>Timestamp when the restriction was released.</summary>
    public DateTime? ReleasedAtUtc { get; private set; }

    /// <summary>Mandatory justification for releasing the restriction.</summary>
    public string? ReleaseReason { get; private set; }

    private ComplianceRestriction() { } // EF Core

    /// <summary>
    /// Creates a new active compliance restriction.
    /// </summary>
    public static ComplianceRestriction Create(
        RiskSubjectType subjectType,
        string subjectId,
        ComplianceRestrictionType restrictionType,
        string reason,
        string placedBy = "System",
        decimal? dailyCapAmount = null,
        decimal? singleCapAmount = null,
        Guid? organizationId = null)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("SubjectId is required.", nameof(subjectId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        return new ComplianceRestriction
        {
            Id = Guid.NewGuid(),
            SubjectType = subjectType,
            SubjectId = subjectId.Trim(),
            OrganizationId = organizationId,
            RestrictionType = restrictionType,
            Reason = reason.Trim(),
            PlacedBy = string.IsNullOrWhiteSpace(placedBy) ? "System" : placedBy.Trim(),
            DailyCapAmount = dailyCapAmount,
            SingleCapAmount = singleCapAmount,
            PlacedAtUtc = DateTime.UtcNow,
            IsActive = true
        };
    }

    /// <summary>
    /// Releases an active compliance restriction with mandatory audit trail.
    /// </summary>
    public void Release(string releaseReason, string releasedByAdminUserId)
    {
        if (!IsActive)
            return;

        if (string.IsNullOrWhiteSpace(releaseReason))
            throw new ArgumentException("ReleaseReason is required.", nameof(releaseReason));
        if (string.IsNullOrWhiteSpace(releasedByAdminUserId))
            throw new ArgumentException("ReleasedByAdminUserId is required.", nameof(releasedByAdminUserId));

        IsActive = false;
        ReleasedBy = releasedByAdminUserId.Trim();
        ReleaseReason = releaseReason.Trim();
        ReleasedAtUtc = DateTime.UtcNow;
    }
}
