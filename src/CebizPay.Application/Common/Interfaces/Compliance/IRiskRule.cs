using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Extensible, deterministic risk rule contract for evaluating customer, organization, and transaction risk factors.
/// </summary>
public interface IRiskRule
{
    /// <summary>Unique deterministic rule identifier (e.g. RULE-PEP-001, RULE-SANCTIONS-001).</summary>
    string RuleId { get; }

    /// <summary>Human-readable name of the risk rule.</summary>
    string RuleName { get; }

    /// <summary>Version of the rule definition.</summary>
    string RulesetVersion { get; }

    /// <summary>Execution precedence (lower executes earlier).</summary>
    int Priority { get; }

    /// <summary>Determines if this rule applies to the specified subject type.</summary>
    bool CanEvaluate(RiskSubjectType subjectType);

    /// <summary>Evaluates the rule against the provided subject context.</summary>
    Task<RiskRuleEvaluationResult> EvaluateAsync(RiskEvaluationContext context, CancellationToken cancellationToken = default);
}
