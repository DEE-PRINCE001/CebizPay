#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Infrastructure.Compliance.Services;

namespace CebizPay.Infrastructure.Compliance.Rules;

/// <summary>
/// Rule evaluating transaction volume and velocity against policy-driven thresholds and baseline risk profiles.
/// Separates statutory regulatory limits from configurable product thresholds and external provider constraints.
/// </summary>
public sealed class TransactionProfileVolumeRule : IRiskRule
{
    private readonly ITransactionLimitPolicyService _limitPolicyService;

    public TransactionProfileVolumeRule(ITransactionLimitPolicyService? limitPolicyService = null)
    {
        _limitPolicyService = limitPolicyService ?? new TransactionLimitPolicyService();
    }

    public string RuleId => "RULE-VOL-001";
    public string RuleName => "Transaction Volume & Velocity Profile Control";
    public string RulesetVersion => _limitPolicyService.GetActivePolicy().Version;
    public int Priority => 25;

    public bool CanEvaluate(RiskSubjectType subjectType) => true;

    public Task<RiskRuleEvaluationResult> EvaluateAsync(RiskEvaluationContext context, CancellationToken cancellationToken = default)
    {
        if (context.SubjectType == RiskSubjectType.Transaction && context.TransactionAmount.HasValue)
        {
            var amount = context.TransactionAmount.Value;
            var policy = _limitPolicyService.GetActivePolicy();
            var isOrg = context.OrganizationId.HasValue || context.Organization != null;

            var eddThreshold = isOrg ? policy.CorporateEddVolumeThreshold : policy.IndividualEddVolumeThreshold;
            var monitoringThreshold = isOrg ? policy.CorporateElevatedMonitoringThreshold : policy.IndividualElevatedMonitoringThreshold;

            if (amount > eddThreshold)
            {
                return Task.FromResult(RiskRuleEvaluationResult.High(
                    RuleId,
                    RuleName,
                    $"Single transaction amount (₦{amount:N2}) exceeds policy threshold (₦{eddThreshold:N2}) under Policy {policy.Version}. Enhanced due diligence and compliance review required.",
                    triggersEdd: true));
            }

            if (amount > monitoringThreshold)
            {
                return Task.FromResult(RiskRuleEvaluationResult.Medium(
                    RuleId,
                    RuleName,
                    $"Elevated single transaction amount (₦{amount:N2}) exceeds monitoring baseline (₦{monitoringThreshold:N2}) under Policy {policy.Version}. Standard monitoring applied.",
                    severity: 2));
            }
        }

        return Task.FromResult(RiskRuleEvaluationResult.Low(
            RuleId,
            RuleName,
            "Transaction amount conforms to expected standard profile."));
    }
}
