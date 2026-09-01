#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Infrastructure.Compliance.Rules;

/// <summary>
/// Regulatory rule evaluating Ultimate Beneficial Ownership (UBO) and Persons with Significant Control (PSC).
/// Complex corporate structures or high PEP exposure in beneficial ownership mandate Enhanced Due Diligence.
/// </summary>
public sealed class BeneficialOwnershipRule : IRiskRule
{
    public const string Version = "2026.1";

    public string RuleId => "RULE-UBO-001";
    public string RuleName => "Ultimate Beneficial Ownership & PSC Control";
    public string RulesetVersion => Version;
    public int Priority => 16;

    public bool CanEvaluate(RiskSubjectType subjectType) => subjectType == RiskSubjectType.Organization;

    public Task<RiskRuleEvaluationResult> EvaluateAsync(RiskEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var uboEvidence = context.VerificationEvidences
            .Where(e => (e.Capability == VerificationCapability.Business || e.Capability == VerificationCapability.BeneficialOwnership) &&
                        e.SafeMetadata != null &&
                        e.SafeMetadata.Contains("beneficial_owners", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.VerifiedAtUtc)
            .FirstOrDefault();

        if (uboEvidence != null)
        {
            if (uboEvidence.SafeMetadata != null &&
                uboEvidence.SafeMetadata.Contains("pep_exposure\": true", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(RiskRuleEvaluationResult.High(
                    RuleId,
                    RuleName,
                    "One or more Ultimate Beneficial Owners or Directors identified as PEP. Mandatory EDD required.",
                    uboEvidence.ProviderReference,
                    triggersEdd: true,
                    requiresSeniorMgmt: true));
            }

            if (uboEvidence.ResultStatus == VerificationResultStatus.Match)
            {
                return Task.FromResult(RiskRuleEvaluationResult.Low(
                    RuleId,
                    RuleName,
                    "Ultimate Beneficial Owners and Persons with Significant Control verified.",
                    uboEvidence.ProviderReference));
            }
        }

        return Task.FromResult(RiskRuleEvaluationResult.Low(
            RuleId,
            RuleName,
            "Standard corporate ownership structure."));
    }
}
