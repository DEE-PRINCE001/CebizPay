using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Finance.Entities;

/// <summary>
/// Domain aggregate representation of an atomic financial transaction.
/// </summary>
public class LedgerTransaction
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Unique financial reference string.</summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>Transaction type taxonomy.</summary>
    public LedgerTransactionType TransactionType { get; private set; }

    /// <summary>Transaction status lifecycle.</summary>
    public LedgerTransactionStatus Status { get; private set; } = LedgerTransactionStatus.Pending;

    /// <summary>Optional idempotency key string.</summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>Transaction description/narrative.</summary>
    public string? Description { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Completion timestamp.</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    private LedgerTransaction() { } // EF Core

    /// <summary>
    /// Creates a new ledger transaction.
    /// </summary>
    public LedgerTransaction(LedgerTransactionType transactionType, string? reference = null, string? idempotencyKey = null, string? description = null)
    {
        Id = Guid.NewGuid();
        Reference = string.IsNullOrWhiteSpace(reference) ? $"TXN-{Guid.NewGuid():N}"[..18].ToUpperInvariant() : reference.Trim();
        TransactionType = transactionType;
        Status = LedgerTransactionStatus.Pending;
        IdempotencyKey = idempotencyKey?.Trim();
        Description = description?.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Completes the transaction.
    /// </summary>
    public void Complete(DateTime now)
    {
        if (Status == LedgerTransactionStatus.Completed) return;
        if (Status == LedgerTransactionStatus.Reversed || Status == LedgerTransactionStatus.Failed)
        {
            throw new InvalidOperationException($"Cannot complete transaction with status {Status}.");
        }

        Status = LedgerTransactionStatus.Completed;
        CompletedAtUtc = now;
    }

    /// <summary>
    /// Marks transaction as failed.
    /// </summary>
    public void Fail(DateTime now)
    {
        if (Status == LedgerTransactionStatus.Failed) return;
        Status = LedgerTransactionStatus.Failed;
        CompletedAtUtc = now;
    }

    /// <summary>
    /// Marks completed transaction as reversed.
    /// </summary>
    public void MarkReversed(DateTime now)
    {
        if (Status == LedgerTransactionStatus.Reversed)
        {
            throw new InvalidOperationException("Transaction has already been reversed.");
        }
        if (Status != LedgerTransactionStatus.Completed)
        {
            throw new InvalidOperationException($"Only completed transactions can be reversed. Current status: {Status}.");
        }

        Status = LedgerTransactionStatus.Reversed;
        CompletedAtUtc = now;
    }
}
