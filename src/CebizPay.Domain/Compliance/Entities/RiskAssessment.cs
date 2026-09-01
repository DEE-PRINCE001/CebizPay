using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Domain.Compliance.Entities;

/// <summary>
/// Immutable snapshot of a deterministic compliance risk assessment for an individual, organization, or transaction.
/// Historical assessments are never overwritten; new evaluations produce new assessment records.
/// </summary>
public class RiskAssessment
{
    private readonly List<RiskFactorResult> _riskFactors = new();

    /// <summary>Unique assessment identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Category of evaluated subject (Individual, Organization, Transaction).</summary>
    public RiskSubjectType SubjectType { get; private set; }

    /// <summary>Subject identifier (UserId, OrganizationId, or TransactionRef).</summary>
    public string SubjectId { get; private set; } = string.Empty;

    /// <summary>Optional organization identifier for tenant scoping.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Overall calculated risk rating (Low, Medium, High, Prohibited).</summary>
    public RiskRating RiskRating { get; private set; }

    /// <summary>Determined Customer Due Diligence (CDD) requirement depth.</summary>
    public CddLevel CddLevel { get; private set; }

    /// <summary>Indicates whether Enhanced Due Diligence (EDD) is mandatorily required.</summary>
    public bool EddRequired { get; private set; }

    /// <summary>Immutable version tag of the active ruleset applied during evaluation.</summary>
    public string RulesetVersion { get; private set; } = string.Empty;

    /// <summary>UTC timestamp when the evaluation was computed.</summary>
    public DateTime EvaluatedAtUtc { get; private set; }

    /// <summary>Optional validity expiration timestamp after which reassessment is due.</summary>
    public DateTime? ExpiresAtUtc { get; private set; }

    /// <summary>Indicates whether this assessment is the active/current evaluation for the subject.</summary>
    public bool IsCurrent { get; private set; } = true;

    /// <summary>Concise, explainable summary of the risk assessment outcome.</summary>
    public string Summary { get; private set; } = string.Empty;

    /// <summary>Granular factors and triggered rules explaining the risk rating.</summary>
    public IReadOnlyCollection<RiskFactorResult> RiskFactors => _riskFactors.AsReadOnly();

    private RiskAssessment() { } // EF Core

    /// <summary>
    /// Creates a new risk assessment.
    /// </summary>
    public static RiskAssessment Create(
        RiskSubjectType subjectType,
        string subjectId,
        RiskRating riskRating,
        CddLevel cddLevel,
        bool eddRequired,
        string rulesetVersion,
        string summary,
        Guid? organizationId = null,
        DateTime? expiresAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("SubjectId is required.", nameof(subjectId));
        if (string.IsNullOrWhiteSpace(rulesetVersion))
            throw new ArgumentException("RulesetVersion is required.", nameof(rulesetVersion));
        if (string.IsNullOrWhiteSpace(summary))
            throw new ArgumentException("Summary is required.", nameof(summary));

        return new RiskAssessment
        {
            Id = Guid.NewGuid(),
            SubjectType = subjectType,
            SubjectId = subjectId.Trim(),
            OrganizationId = organizationId,
            RiskRating = riskRating,
            CddLevel = cddLevel,
            EddRequired = eddRequired,
            RulesetVersion = rulesetVersion.Trim(),
            Summary = summary.Trim(),
            EvaluatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresAtUtc,
            IsCurrent = true
        };
    }

    /// <summary>
    /// Appends an explainable risk factor finding to this assessment.
    /// </summary>
    public void AddRiskFactor(RiskFactorResult factor)
    {
        ArgumentNullException.ThrowIfNull(factor);
        _riskFactors.Add(factor);
    }

    /// <summary>
    /// Marks this assessment as superseded by a newer reassessment evaluation.
    /// </summary>
    public void MarkSuperseded()
    {
        IsCurrent = false;
    }
}
