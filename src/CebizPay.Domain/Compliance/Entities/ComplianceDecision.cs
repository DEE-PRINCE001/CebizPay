using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Domain.Compliance.Entities;

/// <summary>
/// Domain entity recording an authoritative compliance decision for an individual or organization.
/// Tracks whether the decision was automated by the rules engine or resulted from an authorized manual override.
/// Historical decisions are immutable records.
/// </summary>
public class ComplianceDecision
{
    /// <summary>Unique decision identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Subject category (Individual or Organization).</summary>
    public RiskSubjectType SubjectType { get; private set; }

    /// <summary>Subject identifier (UserId or OrganizationId string).</summary>
    public string SubjectId { get; private set; } = string.Empty;

    /// <summary>Optional organization identifier for multi-tenant isolation.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Authoritative compliance decision outcome.</summary>
    public ComplianceDecisionType Decision { get; private set; }

    /// <summary>Associated risk rating when this decision was formed.</summary>
    public RiskRating RiskRating { get; private set; }

    /// <summary>Applied Customer Due Diligence depth level.</summary>
    public CddLevel CddLevel { get; private set; }

    /// <summary>Optional associated EDD status.</summary>
    public EddStatus? EddStatus { get; private set; }

    /// <summary>Detailed explainable reasons for this compliance decision.</summary>
    public string DecisionReasons { get; private set; } = string.Empty;

    /// <summary>Version of the active policy/ruleset applied.</summary>
    public string RulesetVersion { get; private set; } = string.Empty;

    /// <summary>Actor that formed this decision ("System" or Admin UserId).</summary>
    public string DecidedBy { get; private set; } = "System";

    /// <summary>Indicates whether this decision is an administrative manual override.</summary>
    public bool IsManualOverride { get; private set; }

    /// <summary>Mandatory justification if this decision is a manual override.</summary>
    public string? OverrideReason { get; private set; }

    /// <summary>Effective start timestamp.</summary>
    public DateTime EffectiveFromUtc { get; private set; }

    /// <summary>Optional expiration timestamp when decision must be re-evaluated.</summary>
    public DateTime? ExpiresAtUtc { get; private set; }

    /// <summary>Indicates whether this decision is currently active.</summary>
    public bool IsActive { get; private set; } = true;

    private ComplianceDecision() { } // EF Core

    /// <summary>
    /// Creates a new automated compliance decision.
    /// </summary>
    public static ComplianceDecision Create(
        RiskSubjectType subjectType,
        string subjectId,
        ComplianceDecisionType decision,
        RiskRating riskRating,
        CddLevel cddLevel,
        string decisionReasons,
        string rulesetVersion,
        string decidedBy = "System",
        EddStatus? eddStatus = null,
        Guid? organizationId = null,
        DateTime? expiresAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("SubjectId is required.", nameof(subjectId));
        if (string.IsNullOrWhiteSpace(decisionReasons))
            throw new ArgumentException("DecisionReasons is required.", nameof(decisionReasons));
        if (string.IsNullOrWhiteSpace(rulesetVersion))
            throw new ArgumentException("RulesetVersion is required.", nameof(rulesetVersion));

        var now = DateTime.UtcNow;
        return new ComplianceDecision
        {
            Id = Guid.NewGuid(),
            SubjectType = subjectType,
            SubjectId = subjectId.Trim(),
            OrganizationId = organizationId,
            Decision = decision,
            RiskRating = riskRating,
            CddLevel = cddLevel,
            EddStatus = eddStatus,
            DecisionReasons = decisionReasons.Trim(),
            RulesetVersion = rulesetVersion.Trim(),
            DecidedBy = string.IsNullOrWhiteSpace(decidedBy) ? "System" : decidedBy.Trim(),
            IsManualOverride = false,
            EffectiveFromUtc = now,
            ExpiresAtUtc = expiresAtUtc,
            IsActive = true
        };
    }

    /// <summary>
    /// Creates an administrative manual compliance override decision.
    /// Non-negotiable regulatory safeguards (e.g. active sanctions match) cannot be bypassed.
    /// </summary>
    public static ComplianceDecision CreateManualOverride(
        RiskSubjectType subjectType,
        string subjectId,
        ComplianceDecisionType newDecision,
        RiskRating riskRating,
        CddLevel cddLevel,
        string overrideReason,
        string adminUserId,
        string rulesetVersion,
        EddStatus? eddStatus = null,
        Guid? organizationId = null,
        DateTime? expiresAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("SubjectId is required.", nameof(subjectId));
        if (string.IsNullOrWhiteSpace(overrideReason))
            throw new ArgumentException("OverrideReason is required for manual overrides.", nameof(overrideReason));
        if (string.IsNullOrWhiteSpace(adminUserId))
            throw new ArgumentException("AdminUserId is required for manual overrides.", nameof(adminUserId));
        if (string.IsNullOrWhiteSpace(rulesetVersion))
            throw new ArgumentException("RulesetVersion is required.", nameof(rulesetVersion));

        if (riskRating == RiskRating.Prohibited && newDecision == ComplianceDecisionType.Approved)
            throw new InvalidOperationException("Non-negotiable regulatory safeguard: A subject with Prohibited risk rating (confirmed sanctions match) cannot be manually overridden to Approved status.");

        var now = DateTime.UtcNow;
        return new ComplianceDecision
        {
            Id = Guid.NewGuid(),
            SubjectType = subjectType,
            SubjectId = subjectId.Trim(),
            OrganizationId = organizationId,
            Decision = newDecision,
            RiskRating = riskRating,
            CddLevel = cddLevel,
            EddStatus = eddStatus,
            DecisionReasons = $"[MANUAL OVERRIDE by {adminUserId}]: {overrideReason.Trim()}",
            RulesetVersion = rulesetVersion.Trim(),
            DecidedBy = adminUserId.Trim(),
            IsManualOverride = true,
            OverrideReason = overrideReason.Trim(),
            EffectiveFromUtc = now,
            ExpiresAtUtc = expiresAtUtc,
            IsActive = true
        };
    }

    /// <summary>
    /// Deactivates this decision when superseded by a newer decision.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }
}
