using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Savings.Enums;

namespace CebizPay.Application.Common.Interfaces.Savings;

/// <summary>
/// Request payload to ensure or provision a customer profile and KYC mapping at the external provider.
/// </summary>
public record ExternalCustomerRequest(
    string UserId,
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber,
    string? Bvn = null,
    string? Nin = null,
    DateOnly? DateOfBirth = null,
    string? Address = null);

/// <summary>
/// Response returned from the external provider after customer onboarding or profile resolution.
/// </summary>
public record ExternalCustomerResult(
    string ExternalCustomerId,
    string? ExternalWalletOrSubAccountId,
    bool IsKycVerified,
    string? Status = null);

/// <summary>
/// Product quote / rate information for a savings or investment product hosted by the external provider.
/// </summary>
public record ExternalSavingsProductRate(
    string ProductCode,
    SavingsPlanType PlanType,
    decimal AnnualInterestRate,
    int MinDurationDays,
    int MaxDurationDays,
    decimal EarlyPenaltyRate,
    string? Description = null);

/// <summary>
/// Request payload to create an external savings plan, subledger account, or investment portfolio.
/// </summary>
public record ExternalCreatePlanRequest(
    string ExternalCustomerId,
    string PlanName,
    SavingsPlanType PlanType,
    Currency Currency,
    decimal PrincipalAmount,
    int DurationDays,
    DateTime MaturityDateUtc,
    decimal? TargetAmount,
    decimal? ContributionAmount,
    SavingsContributionFrequency? ContributionFrequency,
    string IdempotencyKey);

/// <summary>
/// Response returned from the external provider upon successful plan creation.
/// </summary>
public record ExternalSavingsPlanResult(
    string ExternalPlanId,
    string? ExternalAccountNumber,
    string Status,
    decimal ConfirmedAnnualRate,
    DateTime MaturityDateUtc,
    string? RawResponse = null);

/// <summary>
/// Request payload to deposit / fund capital into an active external savings plan.
/// </summary>
public record ExternalFundingRequest(
    string ExternalPlanId,
    string ExternalCustomerId,
    decimal Amount,
    Currency Currency,
    string TransactionReference,
    string? Description = null);

/// <summary>
/// Response returned from the external provider after funding execution.
/// </summary>
public record ExternalFundingResult(
    string ExternalTransactionId,
    bool IsSettled,
    decimal SettledAmount,
    DateTime SettledAtUtc);

/// <summary>
/// Current live valuation, accrued yield, and maturity state queried from the external provider.
/// </summary>
public record ExternalSavingsPosition(
    string ExternalPlanId,
    decimal PrincipalBalance,
    decimal AccruedInterest,
    decimal TotalYieldEarned,
    bool IsMatured,
    string ExternalStatus,
    DateTime AsOfUtc);

/// <summary>
/// Request payload to liquidate (withdraw) funds from an external savings plan.
/// </summary>
public record ExternalLiquidationRequest(
    string ExternalPlanId,
    string ExternalCustomerId,
    decimal Amount,
    bool IsEarlyExit,
    string TransactionReference,
    string? Reason = null);

/// <summary>
/// Response returned from the external provider after liquidation execution.
/// </summary>
public record ExternalLiquidationResult(
    string ExternalTransactionId,
    decimal GrossPayout,
    decimal PenaltyAmount,
    decimal ForfeitedInterest,
    decimal NetSettledAmount,
    bool IsCompleted,
    DateTime SettledAtUtc);

/// <summary>
/// Standardized event parsed and verified from an inbound external provider webhook.
/// </summary>
public record SavingsWebhookEvent(
    string EventType,
    string ProviderName,
    string? ExternalPlanId,
    string? ExternalCustomerId,
    decimal? Amount,
    DateTime EventTimestampUtc,
    string RawEventId,
    string? Data = null);
