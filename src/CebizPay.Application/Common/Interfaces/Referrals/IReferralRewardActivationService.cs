namespace CebizPay.Application.Common.Interfaces.Referrals;

/// <summary>
/// Architectural boundary abstraction for future referral reward activation.
/// In Phase 6D, financial activation is strictly DISABLED:
/// There is no wallet credit, no ledger posting, and no movement of customer funds.
/// </summary>
public interface IReferralRewardActivationService
{
    /// <summary>
    /// Evaluates or attempts reward activation.
    /// In Phase 6D, this intentionally rejects financial disbursement.
    /// </summary>
    Task<ReferralRewardActivationResult> ActivateRewardAsync(
        Guid rewardId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result contract for reward activation attempts.
/// </summary>
public sealed record ReferralRewardActivationResult(
    bool Succeeded,
    string? LedgerTransactionReference,
    string Message);
