using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Referrals;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Referrals.Entities;
using CebizPay.Domain.Referrals.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Referrals;

/// <summary>
/// Query to retrieve the authenticated user's referral dashboard.
/// </summary>
public sealed record GetReferralDashboardQuery : IRequest<ReferralDashboardDto>;

/// <summary>
/// Handler for GetReferralDashboardQuery.
/// </summary>
public sealed class GetReferralDashboardQueryHandler : IRequestHandler<GetReferralDashboardQuery, ReferralDashboardDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="GetReferralDashboardQueryHandler"/>.
    /// </summary>
    public GetReferralDashboardQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ISender sender)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <inheritdoc/>
    public async Task<ReferralDashboardDto> Handle(GetReferralDashboardQuery request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        // 1. Ensure user has a referral code
        var referralCode = await _sender.Send(new GetOrCreateReferralCodeCommand(), cancellationToken);

        // 2. Load active configuration
        var setting = await _dbContext.ReferralSettings
            .FirstOrDefaultAsync(s => s.IsActive, cancellationToken);

        var configuredReward = setting?.RewardAmountPerSuccessfulReferral ?? 500.00m;
        var maxReferrals = setting?.MaximumSuccessfulReferralsPerUser ?? 10;

        // 3. Load all relationships owned by caller
        var relationships = await _dbContext.ReferralRelationships
            .Where(r => r.ReferrerUserId == callerUserId)
            .OrderByDescending(r => r.RegisteredAtUtc)
            .ToListAsync(cancellationToken);

        var totalReferrals = relationships.Count;
        var qualifiedReferrals = relationships.Count(r => r.QualificationStatus == ReferralQualificationStatus.Qualified);
        var remainingCapacity = Math.Max(0, maxReferrals - qualifiedReferrals);

        // 4. Calculate pending and eligible non-financial reward amounts
        var rewards = await _dbContext.ReferralRewards
            .Where(r => r.ReferrerUserId == callerUserId)
            .ToListAsync(cancellationToken);

        var pendingRewardAmount = rewards
            .Where(r => r.Status == ReferralRewardStatus.Pending || r.Status == ReferralRewardStatus.HeldForRiskReview)
            .Sum(r => r.Amount);

        var eligibleRewardAmount = rewards
            .Where(r => r.Status == ReferralRewardStatus.Eligible)
            .Sum(r => r.Amount);

        // 5. Map referral items with privacy masking
        var referralItems = relationships.Select(r =>
        {
            var maskedId = r.ReferredUserId.Length > 8
                ? $"{r.ReferredUserId[..4]}***{r.ReferredUserId[^4..]}"
                : "User***";

            return new ReferralItemDto(
                r.Id,
                r.ReferredUserId,
                maskedId,
                r.QualificationStatus,
                r.RewardEligibility,
                r.RegisteredAtUtc,
                r.QualifiedAtUtc);
        }).ToList();

        return new ReferralDashboardDto(
            ReferralCode: referralCode,
            TotalReferrals: totalReferrals,
            QualifiedReferrals: qualifiedReferrals,
            RemainingCapacity: remainingCapacity,
            ConfiguredRewardAmount: configuredReward,
            PendingRewardAmount: pendingRewardAmount,
            EligibleRewardAmount: eligibleRewardAmount,
            Referrals: referralItems);
    }
}
