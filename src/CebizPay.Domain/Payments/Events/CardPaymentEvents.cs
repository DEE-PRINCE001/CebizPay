using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Domain.Payments.Events;

/// <summary>Domain event published when a saved card is successfully tokenized and stored.</summary>
public sealed record SavedCardCreatedDomainEvent(
    Guid SavedCardId,
    string UserId,
    Guid WalletId,
    PaymentProvider Provider,
    string Last4,
    string Brand,
    DateTime OccurredOnUtc);

/// <summary>Domain event published when a saved card is revoked by the customer.</summary>
public sealed record SavedCardRevokedDomainEvent(
    Guid SavedCardId,
    string UserId,
    PaymentProvider Provider,
    DateTime OccurredOnUtc);

/// <summary>Domain event published when a saved card token is marked invalid by gateway feedback.</summary>
public sealed record SavedCardInvalidatedDomainEvent(
    Guid SavedCardId,
    string UserId,
    PaymentProvider Provider,
    DateTime OccurredOnUtc);

/// <summary>Domain event published when a card refund is requested.</summary>
public sealed record CardRefundRequestedDomainEvent(
    Guid RefundId,
    Guid FundingTransactionId,
    Guid WalletId,
    decimal Amount,
    Currency Currency,
    PaymentProvider Provider,
    DateTime OccurredOnUtc);

/// <summary>Domain event published when a card refund succeeds and is reversed on the ledger.</summary>
public sealed record CardRefundCompletedDomainEvent(
    Guid RefundId,
    Guid FundingTransactionId,
    Guid WalletId,
    decimal Amount,
    Currency Currency,
    PaymentProvider Provider,
    string? ProviderRefundReference,
    DateTime OccurredOnUtc);

/// <summary>Domain event published when a card refund fails.</summary>
public sealed record CardRefundFailedDomainEvent(
    Guid RefundId,
    Guid FundingTransactionId,
    Guid WalletId,
    decimal Amount,
    Currency Currency,
    PaymentProvider Provider,
    string Reason,
    DateTime OccurredOnUtc);

/// <summary>Domain event published when a card verification session is initiated.</summary>
public sealed record CardVerificationInitiatedDomainEvent(
    Guid VerificationId,
    string UserId,
    Guid WalletId,
    PaymentProvider Provider,
    string Reference,
    DateTime OccurredOnUtc);

/// <summary>Domain event published when a card verification is successfully completed.</summary>
public sealed record CardVerificationCompletedDomainEvent(
    Guid VerificationId,
    string UserId,
    Guid SavedCardId,
    PaymentProvider Provider,
    DateTime OccurredOnUtc);
