using CebizPay.Domain.Referrals.Enums;

namespace CebizPay.Application.UseCases.Referrals;

/// <summary>
/// Authenticated user's referral program dashboard summary.
/// </summary>
public sealed record ReferralDashboardDto(
    string ReferralCode,
    int TotalReferrals,
    int QualifiedReferrals,
    int RemainingCapacity,
    decimal ConfiguredRewardAmount,
    decimal PendingRewardAmount,
    decimal EligibleRewardAmount,
    List<ReferralItemDto> Referrals);

/// <summary>
/// Summary representation of a single referred relationship.
/// </summary>
public sealed record ReferralItemDto(
    Guid Id,
    string ReferredUserId,
    string MaskedIdentity,
    ReferralQualificationStatus Status,
    ReferralRewardEligibility Eligibility,
    DateTime RegisteredAtUtc,
    DateTime? QualifiedAtUtc);

/// <summary>
/// Global referral configuration DTO.
/// </summary>
public sealed record ReferralSettingDto(
    decimal RewardAmountPerSuccessfulReferral,
    int MaximumSuccessfulReferralsPerUser,
    bool IsActive,
    int Version,
    DateTime UpdatedAtUtc,
    string UpdatedBy);

/// <summary>
/// Request payload to claim a referral code upon registration or onboarding.
/// </summary>
public sealed record ClaimReferralCodeRequest(
    string ReferralCode);

/// <summary>
/// Request payload for Super Admin to update global referral configuration.
/// </summary>
public sealed record UpdateReferralSettingRequest(
    decimal RewardAmountPerSuccessfulReferral,
    int MaximumSuccessfulReferralsPerUser,
    bool IsActive);
