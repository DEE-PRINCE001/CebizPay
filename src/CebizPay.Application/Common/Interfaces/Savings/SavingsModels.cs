using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Savings.Enums;

namespace CebizPay.Application.Common.Interfaces.Savings;

/// <summary>
/// Request to preview a savings plan calculation.
/// </summary>
public record SavingsPreviewRequest(
    SavingsPlanType PlanType,
    decimal Amount,
    int DurationDays,
    SavingsContributionFrequency? Frequency = null,
    decimal? TargetAmount = null);

/// <summary>
/// Calculation breakdown result returned from savings preview.
/// </summary>
public record SavingsPreviewResult(
    SavingsPlanType PlanType,
    decimal PrincipalAmount,
    int DurationDays,
    decimal AnnualInterestRate,
    decimal EstimatedTotalInterest,
    decimal EstimatedMaturityPayout,
    decimal EarlyWithdrawalPenaltyRate,
    decimal EstimatedEarlyWithdrawalPenalty,
    decimal EstimatedEarlyWithdrawalNetPayout);

/// <summary>
/// Request to create a new Savings Plan.
/// </summary>
public record CreateSavingsPlanRequest(
    Guid? OrganizationId,
    SavingsOwnerType OwnerType,
    SavingsPlanType PlanType,
    string Name,
    string? Description,
    Currency Currency,
    decimal InterestRate,
    decimal MinimumAmount,
    decimal MaximumAmount,
    int MinimumDurationDays,
    int MaximumDurationDays,
    decimal? TargetAmount = null,
    decimal? ContributionAmount = null,
    SavingsContributionFrequency? ContributionFrequency = null);

/// <summary>
/// Response DTO representing a Savings Plan.
/// </summary>
public record SavingsPlanDto(
    Guid Id,
    Guid? OrganizationId,
    string CreatedByUserId,
    SavingsOwnerType OwnerType,
    SavingsPlanType PlanType,
    string Name,
    string? Description,
    Currency Currency,
    decimal InterestRate,
    decimal MinimumAmount,
    decimal MaximumAmount,
    int MinimumDurationDays,
    int MaximumDurationDays,
    decimal? TargetAmount,
    decimal? ContributionAmount,
    SavingsContributionFrequency? ContributionFrequency,
    int InterestPolicyVersion,
    bool IsActive,
    DateTime CreatedAtUtc);

/// <summary>
/// Request to open / subscribe to a Savings Account.
/// </summary>
public record OpenSavingsAccountRequest(
    Guid SavingsPlanId,
    Guid? OrganizationId,
    decimal InitialDepositAmount,
    int DurationDays,
    decimal? TargetAmount = null,
    decimal? ContributionAmount = null,
    SavingsContributionFrequency? ContributionFrequency = null);

/// <summary>
/// Response DTO representing a Savings Account / Subscription instance.
/// </summary>
public record SavingsAccountDto(
    Guid Id,
    Guid SavingsPlanId,
    string OwnerUserId,
    Guid? OrganizationId,
    Currency Currency,
    SavingsPlanType PlanType,
    decimal PrincipalBalance,
    decimal AccruedInterest,
    decimal TotalInterestWithdrawn,
    SavingsAccountStatus Status,
    decimal InterestRateSnapshot,
    int InterestPolicyVersionSnapshot,
    decimal PenaltyRateSnapshot,
    decimal? TargetAmount,
    decimal? ContributionAmount,
    SavingsContributionFrequency? ContributionFrequency,
    DateTime StartDateUtc,
    DateTime MaturityDateUtc,
    DateTime? MaturedAtUtc,
    DateTime? WithdrawnAtUtc,
    DateTime CreatedAtUtc);

/// <summary>
/// Request to contribute funds to an active savings account.
/// </summary>
public record SavingsContributeRequest(
    decimal Amount,
    string? IdempotencyKey = null);

/// <summary>
/// Request to withdraw funds from a savings account.
/// </summary>
public record SavingsWithdrawRequest(
    string? IdempotencyKey = null);

/// <summary>
/// Response DTO returned after a savings withdrawal execution.
/// </summary>
public record SavingsWithdrawalResultDto(
    Guid SavingsAccountId,
    decimal PayoutAmount,
    decimal PenaltyAmount,
    decimal ForfeitedInterest,
    bool IsEarlyWithdrawal,
    Guid LedgerTransactionId,
    DateTime WithdrawnAtUtc);

/// <summary>
/// Request to configure a Super Admin savings interest policy.
/// </summary>
public record CreateSavingsInterestPolicyRequest(
    SavingsPlanType PlanType,
    GoalInterestPolicyMode Mode,
    decimal AnnualRate);

/// <summary>
/// Response DTO representing a Savings Interest Policy.
/// </summary>
public record SavingsInterestPolicyDto(
    Guid Id,
    SavingsPlanType PlanType,
    GoalInterestPolicyMode Mode,
    decimal AnnualRate,
    int Version,
    DateTime EffectiveFromUtc,
    DateTime? DeactivatedAtUtc,
    bool IsActive);
