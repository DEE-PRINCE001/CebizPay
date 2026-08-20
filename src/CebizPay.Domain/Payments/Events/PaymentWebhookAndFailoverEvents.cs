using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Domain.Payments.Events;

/// <summary>
/// Domain event published when a payment attempt is reconciled to a definitive state via webhook or query.
/// </summary>
public sealed record PaymentAttemptReconciledEvent(
    Guid PaymentAttemptId,
    Guid LedgerTransactionId,
    PaymentProvider Provider,
    int AttemptNumber,
    PaymentAttemptStatus PreviousStatus,
    PaymentAttemptStatus NewStatus,
    string? ProviderReference,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when provider failover is initiated following a technical failure.
/// </summary>
public sealed record ProviderFailoverStartedEvent(
    Guid LedgerTransactionId,
    PaymentProvider FailedProvider,
    PaymentProvider FallbackProvider,
    int PreviousAttemptNumber,
    int NewAttemptNumber,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when provider failover completes successfully.
/// </summary>
public sealed record ProviderFailoverSucceededEvent(
    Guid LedgerTransactionId,
    Guid FallbackAttemptId,
    PaymentProvider FallbackProvider,
    string ProviderReference,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when provider failover fails.
/// </summary>
public sealed record ProviderFailoverFailedEvent(
    Guid LedgerTransactionId,
    PaymentProvider FallbackProvider,
    string FailureReason,
    DateTime OccurredOnUtc);
