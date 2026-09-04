using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Referrals;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Referrals.Entities;
using CebizPay.Domain.Referrals.Enums;
using CebizPay.Domain.Referrals.Events;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Referrals;

/// <summary>
/// Authoritative referral milestone qualification evaluator.
/// Evaluates KYC Tier 1 and qualifying deposit (>= 1,000 NGN) requirements,
/// enforces maximum referrals per user, detects anti-abuse signals,
/// and manages reward eligibility state under strict database transactions.
/// </summary>
public sealed partial class ReferralQualificationService : IReferralQualificationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IOutboxService? _outboxService;
    private readonly ILogger<ReferralQualificationService> _logger;
    private const decimal MinimumQualifyingDeposit = 1000.00m;

    /// <summary>
    /// Initializes a new instance of <see cref="ReferralQualificationService"/>.
    /// </summary>
    public ReferralQualificationService(
        ApplicationDbContext dbContext,
        ILogger<ReferralQualificationService> logger,
        IOutboxService? outboxService = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public async Task<ReferralQualificationEvaluationResult> EvaluateQualificationAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        // 1. Find pending referral relationship for the referred user
        var relationship = await _dbContext.ReferralRelationships
            .FirstOrDefaultAsync(r => r.ReferredUserId == userId && r.QualificationStatus == ReferralQualificationStatus.Pending, cancellationToken);

        if (relationship == null)
        {
            return new ReferralQualificationEvaluationResult(false, false, "No pending referral relationship found for user.");
        }

        // 2. Condition 1: Verify KYC Tier 1 is completed
        var individualProfile = await _dbContext.IndividualProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (individualProfile == null || individualProfile.KycStatus != KycStatus.Verified)
        {
            LogKycPending(_logger, userId);
            return new ReferralQualificationEvaluationResult(false, false, "Referred user KYC Tier 1 is not yet verified.");
        }

        // 3. Condition 2: Verify real qualifying completed deposit of at least ₦1,000
        var userWalletIds = await _dbContext.Wallets
            .Where(w => w.IndividualId == userId)
            .Select(w => w.Id)
            .ToListAsync(cancellationToken);

        var qualifyingDeposit = await _dbContext.FundingTransactions
            .Where(t => userWalletIds.Contains(t.WalletId) &&
                        t.Status == FundingTransactionStatus.Completed &&
                        t.Amount >= MinimumQualifyingDeposit)
            .OrderBy(t => t.CompletedAtUtc ?? t.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (qualifyingDeposit == null)
        {
            LogDepositPending(_logger, userId, MinimumQualifyingDeposit);
            return new ReferralQualificationEvaluationResult(false, false, "Referred user has not completed a qualifying deposit of at least ₦1,000.");
        }

        // 4. Concurrency-safe qualification transaction
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Reload relationship within transaction
            var currentRel = await _dbContext.ReferralRelationships
                .FirstOrDefaultAsync(r => r.Id == relationship.Id, cancellationToken);

            if (currentRel == null || currentRel.QualificationStatus != ReferralQualificationStatus.Pending)
            {
                return new ReferralQualificationEvaluationResult(false, false, "Referral relationship is no longer pending.");
            }

            // Load active configuration snapshot
            var setting = await _dbContext.ReferralSettings
                .FirstOrDefaultAsync(s => s.IsActive, cancellationToken);

            var rewardAmount = setting?.RewardAmountPerSuccessfulReferral ?? 500.00m;
            var maxReferrals = setting?.MaximumSuccessfulReferralsPerUser ?? 10;

            // Count existing qualified referrals for the referring user
            var qualifiedCount = await _dbContext.ReferralRelationships
                .CountAsync(r => r.ReferrerUserId == currentRel.ReferrerUserId &&
                                 r.QualificationStatus == ReferralQualificationStatus.Qualified, cancellationToken);

            var now = DateTime.UtcNow;
            ReferralRewardEligibility eligibility;
            ReferralRewardStatus rewardStatus;
            string? riskNote = null;

            if (qualifiedCount >= maxReferrals)
            {
                // Referral capacity reached: track qualification milestone but exceed reward capacity
                eligibility = ReferralRewardEligibility.CapacityExceeded;
                rewardStatus = ReferralRewardStatus.Rejected;
                riskNote = $"Referring user has reached maximum successful referral cap of {maxReferrals}.";
                LogCapacityReached(_logger, currentRel.ReferrerUserId, maxReferrals);
            }
            else
            {
                // Identity anti-abuse collision check
                var referrerUser = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == currentRel.ReferrerUserId, cancellationToken);
                var referredUser = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == currentRel.ReferredUserId, cancellationToken);

                bool hasPhoneCollision = false;
                if (referrerUser != null && referredUser != null &&
                    !string.IsNullOrWhiteSpace(referrerUser.PhoneNumber) &&
                    !string.IsNullOrWhiteSpace(referredUser.PhoneNumber))
                {
                    var canonicalReferrer = CebizPay.Application.Common.Utils.PhoneNormalizer.NormalizeE164(referrerUser.PhoneNumber);
                    var canonicalReferred = CebizPay.Application.Common.Utils.PhoneNormalizer.NormalizeE164(referredUser.PhoneNumber);
                    hasPhoneCollision = !string.IsNullOrEmpty(canonicalReferrer) && canonicalReferrer == canonicalReferred;
                }

                bool hasEmailCollision = referrerUser != null && referredUser != null &&
                    !string.IsNullOrWhiteSpace(referrerUser.Email) &&
                    referrerUser.Email.Equals(referredUser.Email, StringComparison.OrdinalIgnoreCase);

                bool hasCollision = hasPhoneCollision || hasEmailCollision;

                if (hasCollision)
                {
                    eligibility = ReferralRewardEligibility.HeldForRiskReview;
                    rewardStatus = ReferralRewardStatus.HeldForRiskReview;
                    riskNote = "Identity collision detected between referrer and referred user.";
                    LogCollisionDetected(_logger, currentRel.ReferrerUserId, currentRel.ReferredUserId);
                }
                else
                {
                    eligibility = ReferralRewardEligibility.Eligible;
                    rewardStatus = ReferralRewardStatus.Eligible;
                }
            }

            currentRel.Qualify(
                depositAmount: qualifyingDeposit.Amount,
                depositReference: qualifyingDeposit.ProviderTransactionReference,
                eligibility: eligibility,
                now: now,
                riskNotes: riskNote);

            // Create future reward entitlement record (strictly non-financial in Phase 6D)
            var reward = ReferralReward.Create(
                referralRelationshipId: currentRel.Id,
                referrerUserId: currentRel.ReferrerUserId,
                referredUserId: currentRel.ReferredUserId,
                amount: rewardAmount,
                initialStatus: rewardStatus,
                now: now);

            _dbContext.ReferralRewards.Add(reward);

            _outboxService?.Write(new ReferralQualifiedDomainEvent(
                RelationshipId: currentRel.Id,
                ReferrerUserId: currentRel.ReferrerUserId,
                ReferredUserId: currentRel.ReferredUserId,
                RewardAmount: rewardAmount,
                Eligibility: eligibility,
                OccurredOnUtc: now));

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            LogReferralQualified(_logger, currentRel.Id, currentRel.ReferrerUserId, eligibility);

            return new ReferralQualificationEvaluationResult(
                IsQualified: true,
                RewardEligible: eligibility == ReferralRewardEligibility.Eligible,
                Message: "Referral qualification milestones successfully satisfied.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            LogQualificationError(_logger, userId, ex);
            throw;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Referred user {UserId} KYC Tier 1 is not verified yet.")]
    private static partial void LogKycPending(ILogger logger, string userId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Referred user {UserId} qualifying deposit >= {Amount} NGN is not completed yet.")]
    private static partial void LogDepositPending(ILogger logger, string userId, decimal amount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Referring user {ReferrerUserId} has reached maximum successful referral cap of {MaxReferrals}.")]
    private static partial void LogCapacityReached(ILogger logger, string referrerUserId, int maxReferrals);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Anti-abuse identity collision detected between referrer {ReferrerUserId} and referred user {ReferredUserId}.")]
    private static partial void LogCollisionDetected(ILogger logger, string referrerUserId, string referredUserId);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Referral relationship {RelationshipId} for referrer {ReferrerUserId} qualified with eligibility {Eligibility}.")]
    private static partial void LogReferralQualified(ILogger logger, Guid relationshipId, string referrerUserId, ReferralRewardEligibility eligibility);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Unexpected error evaluating referral qualification for user {UserId}.")]
    private static partial void LogQualificationError(ILogger logger, string userId, Exception exception);
}
