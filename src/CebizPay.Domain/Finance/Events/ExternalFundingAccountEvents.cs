using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Domain.Finance.Events;

/// <summary>
/// Domain event published when an external funding account is attached to a wallet.
/// </summary>
public sealed record ExternalFundingAccountCreatedDomainEvent(
    Guid AccountId,
    Guid WalletId,
    PaymentProvider Provider,
    string AccountNumber,
    string BankCode,
    bool IsPrimary,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when the primary designation of an external funding account changes.
/// </summary>
public sealed record ExternalFundingAccountPrimaryChangedDomainEvent(
    Guid AccountId,
    Guid WalletId,
    PaymentProvider Provider,
    bool IsPrimary,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when an external funding account status is transitioned (e.g. Activated, Suspended, Closed).
/// </summary>
public sealed record ExternalFundingAccountStatusChangedDomainEvent(
    Guid AccountId,
    Guid WalletId,
    ExternalFundingAccountStatus Status,
    DateTime OccurredOnUtc);
