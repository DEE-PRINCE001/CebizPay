using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Loans.Events;
using CebizPay.Domain.Savings.Enums;

namespace CebizPay.Domain.Savings.Events;

/// <summary>Domain event emitted when a new savings plan is created.</summary>
public sealed record SavingsPlanCreatedDomainEvent(
    Guid PlanId,
    Guid? OrganizationId,
    string CreatedByUserId,
    SavingsPlanType PlanType,
    string Name,
    Currency Currency,
    decimal InterestRate) : IDomainEvent;

/// <summary>Domain event emitted when a savings account / instance is opened.</summary>
public sealed record SavingsAccountCreatedDomainEvent(
    Guid SavingsAccountId,
    Guid SavingsPlanId,
    string OwnerUserId,
    Guid? OrganizationId,
    Currency Currency,
    SavingsPlanType PlanType,
    DateTime MaturityDateUtc) : IDomainEvent;

/// <summary>Domain event emitted when a contribution is deposited into a savings account.</summary>
public sealed record SavingsContributionCompletedDomainEvent(
    Guid SavingsAccountId,
    string OwnerUserId,
    decimal Amount,
    Currency Currency,
    Guid LedgerTransactionId) : IDomainEvent;

/// <summary>Domain event emitted when daily interest is accrued for a savings account.</summary>
public sealed record SavingsDailyInterestAccruedDomainEvent(
    Guid SavingsAccountId,
    DateTime AccrualDate,
    decimal Amount,
    decimal TotalAccruedInterest) : IDomainEvent;

/// <summary>Domain event emitted when a savings account reaches maturity.</summary>
public sealed record SavingsMaturedDomainEvent(
    Guid SavingsAccountId,
    string OwnerUserId,
    decimal PrincipalBalance,
    decimal AccruedInterest) : IDomainEvent;

/// <summary>Domain event emitted when a savings account withdrawal is completed.</summary>
public sealed record SavingsWithdrawalCompletedDomainEvent(
    Guid SavingsAccountId,
    string OwnerUserId,
    decimal PayoutAmount,
    decimal PenaltyAmount,
    decimal ForfeitedInterest,
    bool IsEarlyWithdrawal,
    Guid LedgerTransactionId) : IDomainEvent;

/// <summary>Domain event emitted when a savings interest policy is updated / activated.</summary>
public sealed record SavingsInterestPolicyChangedDomainEvent(
    Guid PolicyId,
    SavingsPlanType PlanType,
    GoalInterestPolicyMode Mode,
    decimal AnnualRate,
    int Version) : IDomainEvent;
