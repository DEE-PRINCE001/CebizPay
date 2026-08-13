using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Finance.Events;

/// <summary>
/// Domain event emitted when a ledger transaction completes.
/// </summary>
/// <param name="TransactionId">Ledger transaction ID.</param>
/// <param name="Reference">Transaction reference string.</param>
/// <param name="TransactionType">Transaction type.</param>
/// <param name="OccurredOnUtc">Timestamp when event occurred.</param>
public sealed record LedgerTransactionCompletedDomainEvent(
    Guid TransactionId,
    string Reference,
    LedgerTransactionType TransactionType,
    DateTime OccurredOnUtc);
