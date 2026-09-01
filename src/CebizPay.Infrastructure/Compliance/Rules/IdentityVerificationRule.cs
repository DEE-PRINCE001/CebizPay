#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Infrastructure.Compliance.Rules;

/// <summary>
/// Rule evaluating primary individual identity verification evidence (BVN / NIN).
/// </summary>
public sealed class IdentityVerificationRule : IRiskRule
{
    public const string Version = "2026.1";

    public string RuleId => "RULE-ID-001";
    public string RuleName => "Identity Verification Evidence Control";
    public string RulesetVersion => Version;
    public int Priority => 10;

    public bool CanEvaluate(RiskSubjectType subjectType) => subjectType == RiskSubjectType.Individual;

    public Task<RiskRuleEvaluationResult> EvaluateAsync(RiskEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var idEvidences = context.VerificationEvidences
            .Where(e => e.Capability == VerificationCapability.Identity)
            .OrderByDescending(e => e.VerifiedAtUtc)
            .ToList();

        if (idEvidences.Count == 0)
        {
            return Task.FromResult(RiskRuleEvaluationResult.Medium(
                RuleId,
                RuleName,
                "No external identity verification (BVN/NIN) evidence recorded on profile.",
                severity: 2));
        }

        if (idEvidences.Any(e => e.ResultStatus == VerificationResultStatus.Mismatch))
        {
            return Task.FromResult(RiskRuleEvaluationResult.High(
                RuleId,
                RuleName,
                "External identity provider reported demographic mismatch (name/DOB discrepancy) on BVN/NIN.",
                idEvidences.First(e => e.ResultStatus == VerificationResultStatus.Mismatch).ProviderReference,
                triggersEdd: false));
        }

        if (idEvidences.Any(e => e.ResultStatus == VerificationResultStatus.NotFound))
        {
            return Task.FromResult(RiskRuleEvaluationResult.High(
                RuleId,
                RuleName,
                "Provided identity number was not found in national registry (NIBSS/NIMC).",
                idEvidences.First(e => e.ResultStatus == VerificationResultStatus.NotFound).ProviderReference,
                triggersEdd: false));
        }

        if (idEvidences.Any(e => e.ResultStatus == VerificationResultStatus.Match))
        {
            return Task.FromResult(RiskRuleEvaluationResult.Low(
                RuleId,
                RuleName,
                "Identity credentials successfully verified against national registry.",
                idEvidences.First(e => e.ResultStatus == VerificationResultStatus.Match).ProviderReference));
        }

        return Task.FromResult(RiskRuleEvaluationResult.Medium(
            RuleId,
            RuleName,
            "Identity verification is pending or inconclusive.",
            severity: 2));
    }
}
