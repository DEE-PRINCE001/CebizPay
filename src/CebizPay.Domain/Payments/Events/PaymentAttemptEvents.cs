using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Domain.Payments.Events;

/// <summary>
/// Domain event published when a new payment provider attempt is created.
/// </summary>
public sealed record PaymentAttemptCreatedEvent(
    Guid PaymentAttemptId,
    Guid LedgerTransactionId,
    PaymentProvider Provider,
    int AttemptNumber,
    string RequestReference,
    decimal Amount,
    string Currency,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a payment provider attempt begins active processing.
/// </summary>
public sealed record PaymentAttemptProcessingEvent(
    Guid PaymentAttemptId,
    Guid LedgerTransactionId,
    PaymentProvider Provider,
    int AttemptNumber,
    string RequestReference,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a payment provider attempt succeeds.
/// </summary>
public sealed record PaymentAttemptSucceededEvent(
    Guid PaymentAttemptId,
    Guid LedgerTransactionId,
    PaymentProvider Provider,
    int AttemptNumber,
    string RequestReference,
    string ProviderReference,
    decimal Amount,
    string Currency,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a payment provider attempt definitively fails.
/// </summary>
public sealed record PaymentAttemptFailedEvent(
    Guid PaymentAttemptId,
    Guid LedgerTransactionId,
    PaymentProvider Provider,
    int AttemptNumber,
    string RequestReference,
    string? FailureCode,
    string FailureReason,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a payment provider attempt enters an unknown / timeout state.
/// </summary>
public sealed record PaymentAttemptUnknownEvent(
    Guid PaymentAttemptId,
    Guid LedgerTransactionId,
    PaymentProvider Provider,
    int AttemptNumber,
    string RequestReference,
    string? Reason,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a payment provider attempt is cancelled.
/// </summary>
public sealed record PaymentAttemptCancelledEvent(
    Guid PaymentAttemptId,
    Guid LedgerTransactionId,
    PaymentProvider Provider,
    int AttemptNumber,
    string RequestReference,
    string Reason,
    DateTime OccurredOnUtc);
