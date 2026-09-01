#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Infrastructure.Compliance.Rules;

/// <summary>
/// Rule evaluating negative news and adverse media screening findings.
/// </summary>
public sealed class AdverseMediaScreeningRule : IRiskRule
{
    public const string Version = "2026.1";

    public string RuleId => "RULE-MEDIA-001";
    public string RuleName => "Adverse Media & Negative News Screening";
    public string RulesetVersion => Version;
    public int Priority => 3;

    public bool CanEvaluate(RiskSubjectType subjectType) => true;

    public Task<RiskRuleEvaluationResult> EvaluateAsync(RiskEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var mediaEvidence = context.VerificationEvidences
            .Where(e => e.Capability == VerificationCapability.AmlScreening &&
                        e.SafeMetadata != null &&
                        e.SafeMetadata.Contains("adverse_media", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.VerifiedAtUtc)
            .FirstOrDefault();

        if (mediaEvidence != null)
        {
            if (mediaEvidence.SafeMetadata != null &&
                mediaEvidence.SafeMetadata.Contains("adverse_media_match\": true", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(RiskRuleEvaluationResult.High(
                    RuleId,
                    RuleName,
                    "Adverse media flags identified related to financial crime, fraud, or regulatory enforcement.",
                    mediaEvidence.ProviderReference,
                    triggersEdd: true));
            }
        }

        return Task.FromResult(RiskRuleEvaluationResult.Low(
            RuleId,
            RuleName,
            "No adverse media flags identified."));
    }
}
