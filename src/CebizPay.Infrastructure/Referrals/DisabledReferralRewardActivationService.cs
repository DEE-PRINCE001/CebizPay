using CebizPay.Application.Common.Interfaces.Referrals;

namespace CebizPay.Infrastructure.Referrals;

/// <summary>
/// Authoritative architectural boundary for referral reward financial activation in Phase 6D.
/// Financial rewards are explicitly and strictly DISABLED in Phase 6:
/// There is no wallet credit, no ledger posting, no referral expense ledger account,
/// and no movement of customer funds.
/// </summary>
public sealed class DisabledReferralRewardActivationService : IReferralRewardActivationService
{
    /// <inheritdoc/>
    public Task<ReferralRewardActivationResult> ActivateRewardAsync(
        Guid rewardId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ReferralRewardActivationResult(
            Succeeded: false,
            LedgerTransactionReference: null,
            Message: "Financial reward activation is disabled in Phase 6. No ledger posting or wallet credit occurred."));
    }
}
