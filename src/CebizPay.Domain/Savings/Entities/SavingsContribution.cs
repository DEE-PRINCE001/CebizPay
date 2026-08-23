using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Savings.Entities;

/// <summary>
/// Domain entity representing a financial contribution deposited into a savings account.
/// </summary>
public class SavingsContribution
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Parent savings account ID.</summary>
    public Guid SavingsAccountId { get; private set; }

    /// <summary>Amount deposited.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Currency code.</summary>
    public Currency Currency { get; private set; }

    /// <summary>Ledger transaction ID corresponding to the double-entry wallet debit / savings pool credit.</summary>
    public Guid LedgerTransactionId { get; private set; }

    /// <summary>Idempotency key for repeat-safe financial execution.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>Contribution timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private SavingsContribution() { } // EF Core

    /// <summary>
    /// Creates a new savings contribution record.
    /// </summary>
    public static SavingsContribution Create(
        Guid savingsAccountId,
        decimal amount,
        Currency currency,
        Guid ledgerTransactionId,
        string idempotencyKey)
    {
        if (savingsAccountId == Guid.Empty)
            throw new ArgumentException("SavingsAccountId is required.", nameof(savingsAccountId));
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));
        if (ledgerTransactionId == Guid.Empty)
            throw new ArgumentException("LedgerTransactionId is required.", nameof(ledgerTransactionId));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));

        return new SavingsContribution
        {
            Id = Guid.NewGuid(),
            SavingsAccountId = savingsAccountId,
            Amount = amount,
            Currency = currency,
            LedgerTransactionId = ledgerTransactionId,
            IdempotencyKey = idempotencyKey.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
