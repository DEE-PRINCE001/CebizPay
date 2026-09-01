#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Infrastructure.Compliance.Rules;

/// <summary>
/// Regulatory rule evaluating Politically Exposed Person (PEP) screening evidence.
/// PEP identification mandates High Risk rating, Enhanced Due Diligence (EDD), and Senior Management Approval.
/// </summary>
public sealed class PepScreeningRule : IRiskRule
{
    public const string Version = "2026.1";

    public string RuleId => "RULE-PEP-001";
    public string RuleName => "Politically Exposed Person (PEP) Screening Control";
    public string RulesetVersion => Version;
    public int Priority => 2;

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
                (amlEvidence.SafeMetadata.Contains("pep", StringComparison.OrdinalIgnoreCase) ||
                 amlEvidence.SafeMetadata.Contains("is_pep\": true", StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult(RiskRuleEvaluationResult.High(
                    RuleId,
                    RuleName,
                    "Identified as Politically Exposed Person (PEP) or close associate. Mandatory EDD and Senior Management Approval required under CBN CDD regulations.",
                    amlEvidence.ProviderReference,
                    triggersEdd: true,
                    requiresSeniorMgmt: true));
            }

            if (amlEvidence.ResultStatus == VerificationResultStatus.ReviewRequired)
            {
                return Task.FromResult(RiskRuleEvaluationResult.Medium(
                    RuleId,
                    RuleName,
                    "Possible PEP or watchlist match requiring manual compliance officer investigation.",
                    amlEvidence.ProviderReference,
                    severity: 2));
            }
        }

        return Task.FromResult(RiskRuleEvaluationResult.Low(
            RuleId,
            RuleName,
            "No PEP exposure identified in screening records."));
    }
}
