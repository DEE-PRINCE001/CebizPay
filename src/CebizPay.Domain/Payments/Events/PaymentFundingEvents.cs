using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Domain.Payments.Events;

/// <summary>
/// Domain event raised when a dedicated virtual account is successfully provisioned.
/// </summary>
public sealed record VirtualAccountProvisionedDomainEvent(
    Guid VirtualAccountId,
    string? IndividualId,
    Guid? OrganizationId,
    PaymentProvider Provider,
    string AccountNumber,
    string BankCode,
    Currency Currency,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event raised when a dedicated virtual account status transitions.
/// </summary>
public sealed record VirtualAccountStatusChangedDomainEvent(
    Guid VirtualAccountId,
    VirtualAccountStatus OldStatus,
    VirtualAccountStatus NewStatus,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event raised when an inbound virtual account deposit is received and credited.
/// </summary>
public sealed record InboundVirtualAccountDepositCompletedDomainEvent(
    Guid FundingTransactionId,
    Guid WalletId,
    Guid VirtualAccountId,
    Guid LedgerTransactionId,
    decimal Amount,
    Currency Currency,
    PaymentProvider Provider,
    string ProviderTransactionReference,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event raised when an inbound deposit via ExternalFundingAccount (e.g. Monnify Reserved Virtual Account) is credited.
/// </summary>
public sealed record ExternalFundingAccountDepositCompletedDomainEvent(
    Guid FundingTransactionId,
    Guid WalletId,
    Guid ExternalFundingAccountId,
    Guid LedgerTransactionId,
    decimal GrossAmount,
    decimal FeeAmount,
    decimal NetCreditedAmount,
    Currency Currency,
    PaymentProvider Provider,
    string ProviderTransactionReference,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event raised when a card funding checkout session is initiated.
/// </summary>
public sealed record CardFundingInitiatedDomainEvent(
    Guid FundingTransactionId,
    Guid WalletId,
    decimal Amount,
    Currency Currency,
    PaymentProvider Provider,
    string ProviderTransactionReference,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event raised when a card funding transaction is completed and credited to the wallet.
/// </summary>
public sealed record CardFundingCompletedDomainEvent(
    Guid FundingTransactionId,
    Guid WalletId,
    Guid LedgerTransactionId,
    decimal Amount,
    Currency Currency,
    PaymentProvider Provider,
    string ProviderTransactionReference,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event raised when a card funding transaction fails.
/// </summary>
public sealed record CardFundingFailedDomainEvent(
    Guid FundingTransactionId,
    Guid WalletId,
    decimal Amount,
    Currency Currency,
    PaymentProvider Provider,
    string ProviderTransactionReference,
    string Reason,
    DateTime OccurredOnUtc);
