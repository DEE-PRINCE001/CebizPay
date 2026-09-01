#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Compliance.Events;
using CebizPay.Domain.Finance.Enums;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Compliance.Services;

/// <summary>
/// Financial enforcement boundary evaluating transaction compliance eligibility before execution.
/// Evaluates layered constraints: Non-overridable CBN Regulatory Ceilings, CebizPay Product Policies,
/// Provider Rail Limits, and Customer-specific Risk Restrictions.
/// Ensures fail-closed gating without modifying ledger or wallet balances directly.
/// </summary>
public sealed class ComplianceEligibilityService : IComplianceEligibilityService
{
    private static readonly string[] KycRejectedRestrictions = new[] { "User KYC rejected." };
    private static readonly string[] KybRejectedRestrictions = new[] { "Organization KYB rejected." };

    private readonly IApplicationDbContext _dbContext;
    private readonly IOutboxService _outboxService;
    private readonly RiskMetrics _metrics;
    private readonly ITransactionLimitPolicyService _limitPolicyService;

    public ComplianceEligibilityService(
        IApplicationDbContext dbContext,
        IOutboxService outboxService,
        RiskMetrics metrics,
        ITransactionLimitPolicyService? limitPolicyService = null)
    {
        _dbContext = dbContext;
        _outboxService = outboxService;
        _metrics = metrics;
        _limitPolicyService = limitPolicyService ?? new TransactionLimitPolicyService();
    }

    public async Task<TransactionEligibilityResult> EvaluateEligibilityAsync(
        string userId,
        Guid? organizationId,
        ComplianceOperationType operationType,
        decimal amount,
        Currency currency,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));

        // 1. Evaluate User Compliance Decision
        var userDecision = await _dbContext.ComplianceDecisions
            .AsNoTracking()
            .Where(d => d.SubjectType == RiskSubjectType.Individual && d.SubjectId == userId && d.IsActive)
            .OrderByDescending(d => d.EffectiveFromUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (userDecision != null)
        {
            if (userDecision.Decision == ComplianceDecisionType.Suspended)
            {
                return FailClosed(userId, organizationId, operationType, amount, currency,
                    TransactionEligibilityResult.Suspended("User account is suspended by compliance."));
            }

            if (userDecision.Decision == ComplianceDecisionType.Rejected)
            {
                return FailClosed(userId, organizationId, operationType, amount, currency,
                    TransactionEligibilityResult.Restricted("User compliance verification has been rejected.", KycRejectedRestrictions));
            }

            if (userDecision.Decision == ComplianceDecisionType.EddRequired && IsOutbound(operationType))
            {
                return FailClosed(userId, organizationId, operationType, amount, currency,
                    TransactionEligibilityResult.EddRequired("Enhanced Due Diligence (EDD) must be completed before executing outbound payouts."));
            }
        }

        // 2. Evaluate Organization Compliance Decision if applicable
        if (organizationId.HasValue)
        {
            var orgIdStr = organizationId.Value.ToString();
            var orgDecision = await _dbContext.ComplianceDecisions
                .AsNoTracking()
                .Where(d => d.SubjectType == RiskSubjectType.Organization && d.SubjectId == orgIdStr && d.IsActive)
                .OrderByDescending(d => d.EffectiveFromUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (orgDecision != null)
            {
                if (orgDecision.Decision == ComplianceDecisionType.Suspended)
                {
                    return FailClosed(userId, organizationId, operationType, amount, currency,
                        TransactionEligibilityResult.Suspended("Organization account is suspended by compliance."));
                }

                if (orgDecision.Decision == ComplianceDecisionType.Rejected)
                {
                    return FailClosed(userId, organizationId, operationType, amount, currency,
                        TransactionEligibilityResult.Restricted("Organization KYB verification has been rejected.", KybRejectedRestrictions));
                }

                if (orgDecision.Decision == ComplianceDecisionType.EddRequired && IsOutbound(operationType))
                {
                    return FailClosed(userId, organizationId, operationType, amount, currency,
                        TransactionEligibilityResult.EddRequired("Organization Enhanced Due Diligence (EDD) must be completed before executing outbound payouts."));
                }
            }
        }

        // 3. Evaluate Active Compliance Restrictions (Account-Level & Channel Locks)
        var activeRestrictions = await _dbContext.ComplianceRestrictions
            .AsNoTracking()
            .Where(r => r.IsActive && (r.SubjectId == userId || (organizationId.HasValue && r.SubjectId == organizationId.Value.ToString())))
            .ToListAsync(cancellationToken);

        decimal? customerSpecificSingleCap = null;

        foreach (var restriction in activeRestrictions)
        {
            if (restriction.RestrictionType == ComplianceRestrictionType.FullAccountSuspension)
            {
                return FailClosed(userId, organizationId, operationType, amount, currency,
                    TransactionEligibilityResult.Suspended($"Operation blocked by active restriction: {restriction.Reason}"));
            }

            if (restriction.RestrictionType == ComplianceRestrictionType.BlockAllOutbound && IsOutbound(operationType))
            {
                return FailClosed(userId, organizationId, operationType, amount, currency,
                    TransactionEligibilityResult.Restricted($"All outbound operations blocked by compliance: {restriction.Reason}", new[] { restriction.Reason }));
            }

            if (restriction.RestrictionType == ComplianceRestrictionType.BlockBankTransfer && operationType == ComplianceOperationType.BankTransferPayout)
            {
                return FailClosed(userId, organizationId, operationType, amount, currency,
                    TransactionEligibilityResult.Restricted($"Bank transfers blocked by compliance: {restriction.Reason}", new[] { restriction.Reason }));
            }

            if (restriction.RestrictionType == ComplianceRestrictionType.BlockCardFunding && operationType == ComplianceOperationType.CardFunding)
            {
                return FailClosed(userId, organizationId, operationType, amount, currency,
                    TransactionEligibilityResult.Restricted($"Card funding blocked by compliance: {restriction.Reason}", new[] { restriction.Reason }));
            }

            if (restriction.RestrictionType == ComplianceRestrictionType.BlockVirtualAccount && operationType == ComplianceOperationType.VirtualAccountFunding)
            {
                return FailClosed(userId, organizationId, operationType, amount, currency,
                    TransactionEligibilityResult.Restricted($"Virtual account funding blocked by compliance: {restriction.Reason}", new[] { restriction.Reason }));
            }

            if (restriction.RestrictionType == ComplianceRestrictionType.CapSingleTransaction && restriction.SingleCapAmount.HasValue)
            {
                customerSpecificSingleCap = customerSpecificSingleCap.HasValue
                    ? Math.Min(customerSpecificSingleCap.Value, restriction.SingleCapAmount.Value)
                    : restriction.SingleCapAmount.Value;
            }
        }

        // 4. Layered Policy Limit Evaluation (Regulatory Ceilings, Product Policies, Provider Rails, Customer Caps)
        var subjectType = organizationId.HasValue ? RiskSubjectType.Organization : RiskSubjectType.Individual;
        int? individualTier = null;

        if (subjectType == RiskSubjectType.Individual)
        {
            var cdd = await _dbContext.CddProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.SubjectType == RiskSubjectType.Individual && c.SubjectId == userId, cancellationToken);

            individualTier = cdd?.Tier ?? 1;
        }

        var effectiveLimit = _limitPolicyService.CalculateEffectiveLimit(
            subjectType,
            individualTier,
            operationType,
            customerSpecificSingleCap);

        if (amount > effectiveLimit.EffectiveSingleCap)
        {
            var constraintReason = $"{effectiveLimit.Explanation} (Policy {effectiveLimit.PolicyVersion})";
            return FailClosed(
                userId,
                organizationId,
                operationType,
                amount,
                currency,
                TransactionEligibilityResult.Restricted(
                    constraintReason,
                    new[] { effectiveLimit.BindingConstraintSource.ToString() },
                    effectiveLimit.EffectiveSingleCap));
        }

        // All checks passed
        var result = TransactionEligibilityResult.Allowed(effectiveLimit.EffectiveSingleCap);
        _outboxService.Write(new TransactionEligibilityEvaluatedDomainEvent(
            userId,
            organizationId,
            operationType,
            amount,
            currency,
            result.Status,
            null,
            DateTime.UtcNow));

        return result;
    }

    private TransactionEligibilityResult FailClosed(
        string userId,
        Guid? organizationId,
        ComplianceOperationType operationType,
        decimal amount,
        Currency currency,
        TransactionEligibilityResult result)
    {
        _metrics.RecordEligibilityRejection(operationType, result.Status);

        _outboxService.Write(new TransactionEligibilityEvaluatedDomainEvent(
            userId,
            organizationId,
            operationType,
            amount,
            currency,
            result.Status,
            result.RestrictionReason,
            DateTime.UtcNow));

        return result;
    }

    private static bool IsOutbound(ComplianceOperationType operationType) =>
        operationType == ComplianceOperationType.BankTransferPayout ||
        operationType == ComplianceOperationType.SalaryDisbursement ||
        operationType == ComplianceOperationType.PeerTransfer ||
        operationType == ComplianceOperationType.VasPurchase;
}
