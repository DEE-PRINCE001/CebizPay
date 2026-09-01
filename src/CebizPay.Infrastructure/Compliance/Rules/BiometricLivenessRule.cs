#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Infrastructure.Compliance.Rules;

/// <summary>
/// Rule evaluating biometric selfie liveness and 1:1 facial match evidence.
/// </summary>
public sealed class BiometricLivenessRule : IRiskRule
{
    public const string Version = "2026.1";

    public string RuleId => "RULE-BIO-001";
    public string RuleName => "Biometric Liveness & Face Match Control";
    public string RulesetVersion => Version;
    public int Priority => 20;

    public bool CanEvaluate(RiskSubjectType subjectType) => subjectType == RiskSubjectType.Individual;

    public Task<RiskRuleEvaluationResult> EvaluateAsync(RiskEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var bioEvidence = context.VerificationEvidences
            .Where(e => e.Capability == VerificationCapability.Biometrics)
            .OrderByDescending(e => e.VerifiedAtUtc)
            .FirstOrDefault();

        if (bioEvidence == null)
        {
            return Task.FromResult(RiskRuleEvaluationResult.Low(
                RuleId,
                RuleName,
                "No biometric verification submitted (not mandatory for Tier 1)."));
        }

        if (bioEvidence.ResultStatus == VerificationResultStatus.Mismatch)
        {
            return Task.FromResult(RiskRuleEvaluationResult.High(
                RuleId,
                RuleName,
                "Biometric selfie failed 1:1 face match comparison against registry image or liveness check failed.",
                bioEvidence.ProviderReference,
                triggersEdd: false));
        }

        if (bioEvidence.ResultStatus == VerificationResultStatus.Match)
        {
            return Task.FromResult(RiskRuleEvaluationResult.Low(
                RuleId,
                RuleName,
                $"Biometric liveness and facial match confirmed with confidence {bioEvidence.ConfidenceScore:F1}%.",
                bioEvidence.ProviderReference));
        }

        return Task.FromResult(RiskRuleEvaluationResult.Medium(
            RuleId,
            RuleName,
            "Biometric verification outcome inconclusive or requiring review.",
            bioEvidence.ProviderReference,
            severity: 2));
    }
}
