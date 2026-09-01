#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Infrastructure.Compliance.Rules;

/// <summary>
/// Regulatory rule evaluating Corporate Affairs Commission (CAC) legal entity registration for organizations.
/// Under CBN CDD regulations, legal persons must have verified registration status, RC number, and active entity state.
/// </summary>
public sealed class CacCorporateRegistryRule : IRiskRule
{
    public const string Version = "2026.1";

    public string RuleId => "RULE-CAC-001";
    public string RuleName => "Corporate Affairs Commission (CAC) Legal Entity Verification";
    public string RulesetVersion => Version;
    public int Priority => 15;

    public bool CanEvaluate(RiskSubjectType subjectType) => subjectType == RiskSubjectType.Organization;

    public Task<RiskRuleEvaluationResult> EvaluateAsync(RiskEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var cacEvidence = context.VerificationEvidences
            .Where(e => e.Capability == VerificationCapability.Business)
            .OrderByDescending(e => e.VerifiedAtUtc)
            .FirstOrDefault();

        if (cacEvidence == null)
        {
            return Task.FromResult(RiskRuleEvaluationResult.Medium(
                RuleId,
                RuleName,
                "No external CAC business verification evidence recorded for organization.",
                severity: 2));
        }

        if (cacEvidence.ResultStatus == VerificationResultStatus.Mismatch ||
            cacEvidence.ResultStatus == VerificationResultStatus.NotFound)
        {
            return Task.FromResult(RiskRuleEvaluationResult.High(
                RuleId,
                RuleName,
                "Corporate registry verification failed or RC number not found with CAC.",
                cacEvidence.ProviderReference,
                triggersEdd: true));
        }

        if (cacEvidence.ResultStatus == VerificationResultStatus.Match)
        {
            return Task.FromResult(RiskRuleEvaluationResult.Low(
                RuleId,
                RuleName,
                "Corporate registration and active status verified with Corporate Affairs Commission (CAC).",
                cacEvidence.ProviderReference));
        }

        return Task.FromResult(RiskRuleEvaluationResult.Medium(
            RuleId,
            RuleName,
            "CAC business verification status is pending or requires manual review.",
            cacEvidence.ProviderReference,
            severity: 2));
    }
}
