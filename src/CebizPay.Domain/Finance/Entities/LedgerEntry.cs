using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Finance.Entities;

/// <summary>
/// Domain entity representing an immutable double-entry record.
/// Rules: Amount > 0, Currency must equal LedgerAccount.Currency. No UPDATE or DELETE.
/// </summary>
public class LedgerEntry
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Parent LedgerTransaction ID.</summary>
    public Guid LedgerTransactionId { get; private set; }

    /// <summary>Target LedgerAccount ID.</summary>
    public Guid LedgerAccountId { get; private set; }

    /// <summary>Entry direction (Debit or Credit).</summary>
    public LedgerEntryDirection Direction { get; private set; }

    /// <summary>Monetary amount (must be positive).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Currency code.</summary>
    public Currency Currency { get; private set; }

    /// <summary>Sequence index within parent transaction.</summary>
    public int Sequence { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private LedgerEntry() { } // EF Core

    /// <summary>
    /// Creates a new immutable ledger entry.
    /// </summary>
    public LedgerEntry(Guid ledgerTransactionId, Guid ledgerAccountId, LedgerEntryDirection direction, decimal amount, Currency currency, int sequence = 1)
    {
        if (ledgerTransactionId == Guid.Empty)
            throw new ArgumentException("LedgerTransactionId is required.", nameof(ledgerTransactionId));
        if (ledgerAccountId == Guid.Empty)
            throw new ArgumentException("LedgerAccountId is required.", nameof(ledgerAccountId));
        if (amount <= 0)
            throw new ArgumentException("Ledger entry amount must be positive.", nameof(amount));

        Id = Guid.NewGuid();
        LedgerTransactionId = ledgerTransactionId;
        LedgerAccountId = ledgerAccountId;
        Direction = direction;
        Amount = amount;
        Currency = currency;
        Sequence = sequence;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
