namespace CebizPay.Application.Common.Interfaces.Referrals;

/// <summary>
/// Service contract for evaluating referral qualification milestones.
/// Evaluates whether a referred user has completed both KYC Tier 1 and minimum qualifying deposit.
/// </summary>
public interface IReferralQualificationService
{
    /// <summary>
    /// Evaluates qualification requirements for a referred user and transitions referral/reward state.
    /// Idempotent and thread-safe against concurrent qualification attempts.
    /// </summary>
    Task<ReferralQualificationEvaluationResult> EvaluateQualificationAsync(
        string userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Evaluation result from referral qualification check.
/// </summary>
public sealed record ReferralQualificationEvaluationResult(
    bool IsQualified,
    bool RewardEligible,
    string Message);
