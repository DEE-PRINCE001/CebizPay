namespace CebizPay.Domain.Finance.Events;

/// <summary>
/// Domain event emitted when a transaction is reversed.
/// </summary>
/// <param name="OriginalTransactionId">Original transaction ID.</param>
/// <param name="ReversalTransactionId">Reversal transaction ID.</param>
/// <param name="Reason">Reason for reversal.</param>
/// <param name="OccurredOnUtc">Timestamp when event occurred.</param>
public sealed record LedgerTransactionReversedDomainEvent(
    Guid OriginalTransactionId,
    Guid ReversalTransactionId,
    string Reason,
    DateTime OccurredOnUtc);
