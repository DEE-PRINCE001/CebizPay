#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Infrastructure.Compliance.Rules;

/// <summary>
/// Non-negotiable regulatory rule evaluating AML Sanctions screening evidence.
/// Confirmed sanctions match results in immediate Prohibited status and financial hold.
/// </summary>
public sealed class SanctionsScreeningRule : IRiskRule
{
    public const string Version = "2026.1";

    public string RuleId => "RULE-SANCTIONS-001";
    public string RuleName => "AML Sanctions Screening Control";
    public string RulesetVersion => Version;
    public int Priority => 1; // Highest non-negotiable regulatory priority

    public bool CanEvaluate(RiskSubjectType subjectType) => true;

    public Task<RiskRuleEvaluationResult> EvaluateAsync(RiskEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var amlEvidence = context.VerificationEvidences
            .Where(e => e.Capability == VerificationCapability.AmlScreening)
            .OrderByDescending(e => e.VerifiedAtUtc)
            .FirstOrDefault();

        if (amlEvidence != null)
        {
            if (amlEvidence.ResultStatus == VerificationResultStatus.Match &&
                amlEvidence.SafeMetadata != null &&
                amlEvidence.SafeMetadata.Contains("sanction", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(RiskRuleEvaluationResult.Prohibited(
                    RuleId,
                    RuleName,
                    "Confirmed match against international or national sanctions watchlist.",
                    amlEvidence.ProviderReference));
            }
        }

        return Task.FromResult(RiskRuleEvaluationResult.Low(
            RuleId,
            RuleName,
            "No active sanctions match identified."));
    }
}
