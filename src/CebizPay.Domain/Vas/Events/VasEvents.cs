using CebizPay.Domain.Vas.Enums;

namespace CebizPay.Domain.Vas.Events;

/// <summary>Published when a new VAS purchase transaction is created and financial debit posted.</summary>
public sealed record VasPurchaseCreatedEvent(
    Guid VasTransactionId,
    string Reference,
    string UserId,
    Guid? OrganizationId,
    Guid WalletId,
    Guid LedgerTransactionId,
    VasType Type,
    VasNetwork Network,
    string MaskedPhoneNumber,
    decimal Amount,
    string Currency,
    string? ProductCode,
    DateTime OccurredOnUtc);

/// <summary>Published when VAS transaction begins active gateway dispatch.</summary>
public sealed record VasPurchaseProcessingEvent(
    Guid VasTransactionId,
    string Reference,
    VasProvider Provider,
    DateTime OccurredOnUtc);

/// <summary>Published when a VAS transaction fulfills successfully at the gateway.</summary>
public sealed record VasPurchaseSucceededEvent(
    Guid VasTransactionId,
    string Reference,
    string ProviderReference,
    decimal Amount,
    string Currency,
    DateTime OccurredOnUtc);

/// <summary>Published when a VAS transaction definitively fails at the gateway.</summary>
public sealed record VasPurchaseFailedEvent(
    Guid VasTransactionId,
    string Reference,
    string? FailureCode,
    string FailureReason,
    DateTime OccurredOnUtc);

/// <summary>Published when a VAS transaction encounters an indeterminate/timeout outcome.</summary>
public sealed record VasPurchaseUnknownEvent(
    Guid VasTransactionId,
    string Reference,
    string Reason,
    DateTime OccurredOnUtc);

/// <summary>Published when a failed VAS transaction has been financially reversed back to the wallet.</summary>
public sealed record VasPurchaseReversedEvent(
    Guid VasTransactionId,
    string Reference,
    string Reason,
    decimal Amount,
    string Currency,
    DateTime OccurredOnUtc);

/// <summary>Published when a VAS transaction is reconciled via background status query.</summary>
public sealed record VasPurchaseReconciledEvent(
    Guid VasTransactionId,
    string Reference,
    VasTransactionStatus PreviousStatus,
    VasTransactionStatus NewStatus,
    string? ProviderReference,
    DateTime OccurredOnUtc);
