using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Domain.Compliance.Entities;

/// <summary>
/// Immutable record of an individual deterministic risk rule evaluation within a risk assessment.
/// Provides explainability and audit trail for why a specific risk score or rating was assigned.
/// </summary>
public class RiskFactorResult
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Parent risk assessment identifier.</summary>
    public Guid RiskAssessmentId { get; private set; }

    /// <summary>Deterministic rule identifier (e.g. RULE-PEP-001, RULE-SANCTIONS-001).</summary>
    public string RuleId { get; private set; } = string.Empty;

    /// <summary>Human-readable name of the evaluated rule.</summary>
    public string RuleName { get; private set; } = string.Empty;

    /// <summary>Risk rating contribution from this specific rule.</summary>
    public RiskRating RiskRating { get; private set; }

    /// <summary>Explainable justification for the rule finding.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Optional reference to underlying verification evidence or profile attribute.</summary>
    public string? EvidenceReference { get; private set; }

    /// <summary>Severity weight of the factor (1 = Low, 2 = Medium, 3 = High, 4 = Critical/Prohibitive).</summary>
    public int Severity { get; private set; }

    private RiskFactorResult() { } // EF Core

    /// <summary>
    /// Creates a new risk factor evaluation result.
    /// </summary>
    public static RiskFactorResult Create(
        Guid riskAssessmentId,
        string ruleId,
        string ruleName,
        RiskRating riskRating,
        string reason,
        string? evidenceReference = null,
        int severity = 1)
    {
        if (riskAssessmentId == Guid.Empty)
            throw new ArgumentException("RiskAssessmentId is required.", nameof(riskAssessmentId));
        if (string.IsNullOrWhiteSpace(ruleId))
            throw new ArgumentException("RuleId is required.", nameof(ruleId));
        if (string.IsNullOrWhiteSpace(ruleName))
            throw new ArgumentException("RuleName is required.", nameof(ruleName));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        return new RiskFactorResult
        {
            Id = Guid.NewGuid(),
            RiskAssessmentId = riskAssessmentId,
            RuleId = ruleId.Trim(),
            RuleName = ruleName.Trim(),
            RiskRating = riskRating,
            Reason = reason.Trim(),
            EvidenceReference = evidenceReference?.Trim(),
            Severity = Math.Clamp(severity, 1, 4)
        };
    }
}
