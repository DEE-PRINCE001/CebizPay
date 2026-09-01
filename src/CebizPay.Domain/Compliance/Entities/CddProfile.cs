using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Domain.Compliance.Entities;

/// <summary>
/// Domain entity managing the live Customer Due Diligence (CDD) profile and regulatory lifecycle state.
/// Strictly segregates natural persons (Individuals) and legal entities (Organizations).
/// </summary>
public class CddProfile
{
    /// <summary>Unique profile identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Subject category (Individual or Organization).</summary>
    public RiskSubjectType SubjectType { get; private set; }

    /// <summary>Subject identifier (UserId or OrganizationId string).</summary>
    public string SubjectId { get; private set; } = string.Empty;

    /// <summary>Optional organization identifier for multi-tenant isolation.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Current CDD lifecycle status.</summary>
    public CddStatus Status { get; private set; } = CddStatus.NotStarted;

    /// <summary>Current authoritative risk rating.</summary>
    public RiskRating RiskRating { get; private set; } = RiskRating.Medium;

    /// <summary>Required CDD verification depth level.</summary>
    public CddLevel CddLevel { get; private set; } = CddLevel.Standard;

    /// <summary>Optional individual KYC verification tier (1, 2, or 3). Null for legal entities.</summary>
    public int? Tier { get; private set; }

    /// <summary>Pointer to the latest active RiskAssessment record.</summary>
    public Guid? LatestRiskAssessmentId { get; private set; }

    /// <summary>Timestamp when CDD was fully completed.</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Timestamp of last risk or CDD evaluation.</summary>
    public DateTime LastEvaluatedAtUtc { get; private set; }

    /// <summary>Optional compliance officer review comments or escalation notes.</summary>
    public string? ReviewNotes { get; private set; }

    private CddProfile() { } // EF Core

    /// <summary>
    /// Creates a newly initialized CDD profile.
    /// </summary>
    public static CddProfile Create(
        RiskSubjectType subjectType,
        string subjectId,
        Guid? organizationId = null,
        int? tier = null)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("SubjectId is required.", nameof(subjectId));

        if (subjectType == RiskSubjectType.Organization && tier.HasValue)
            throw new ArgumentException("Tiered KYC applies to individuals only and does not apply to legal persons.", nameof(tier));

        var now = DateTime.UtcNow;
        return new CddProfile
        {
            Id = Guid.NewGuid(),
            SubjectType = subjectType,
            SubjectId = subjectId.Trim(),
            OrganizationId = organizationId,
            Status = CddStatus.NotStarted,
            RiskRating = RiskRating.Medium,
            CddLevel = CddLevel.Standard,
            Tier = tier,
            LastEvaluatedAtUtc = now
        };
    }

    /// <summary>
    /// Updates the CDD profile state from an evaluated risk assessment.
    /// </summary>
    public void UpdateFromAssessment(RiskAssessment assessment, int? tier = null)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        LatestRiskAssessmentId = assessment.Id;
        RiskRating = assessment.RiskRating;
        CddLevel = assessment.CddLevel;
        LastEvaluatedAtUtc = DateTime.UtcNow;

        if (SubjectType == RiskSubjectType.Individual && tier.HasValue)
        {
            Tier = tier.Value;
        }

        if (assessment.RiskRating == RiskRating.Prohibited)
        {
            Status = CddStatus.Suspended;
            ReviewNotes = assessment.Summary;
            return;
        }

        if (assessment.EddRequired)
        {
            Status = CddStatus.EnhancedRequired;
            ReviewNotes = assessment.Summary;
            return;
        }

        if (assessment.RiskRating == RiskRating.High)
        {
            Status = CddStatus.ReviewRequired;
            ReviewNotes = assessment.Summary;
            return;
        }

        Status = CddStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        ReviewNotes = assessment.Summary;
    }

    /// <summary>
    /// Marks CDD as completed.
    /// </summary>
    public void MarkCompleted(string? notes = null)
    {
        Status = CddStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        LastEvaluatedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(notes))
            ReviewNotes = notes.Trim();
    }

    /// <summary>
    /// Marks CDD as requiring compliance review.
    /// </summary>
    public void MarkReviewRequired(string reason)
    {
        Status = CddStatus.ReviewRequired;
        ReviewNotes = reason.Trim();
        LastEvaluatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks CDD as requiring Enhanced Due Diligence (EDD).
    /// </summary>
    public void MarkEnhancedRequired(string reason)
    {
        Status = CddStatus.EnhancedRequired;
        CddLevel = CddLevel.Enhanced;
        ReviewNotes = reason.Trim();
        LastEvaluatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Suspends CDD profile due to sanctions or prohibitive compliance breaches.
    /// </summary>
    public void MarkSuspended(string reason)
    {
        Status = CddStatus.Suspended;
        ReviewNotes = reason.Trim();
        LastEvaluatedAtUtc = DateTime.UtcNow;
    }
}
