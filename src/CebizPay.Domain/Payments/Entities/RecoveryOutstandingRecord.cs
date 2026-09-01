#pragma warning disable CS1591
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Domain.Payments.Entities;

/// <summary>
/// Durable entity tracking outstanding financial claims (e.g. uncollectible card refund reversals,
/// chargeback shortfalls) where wallet balance was insufficient for immediate full debit.
/// Guarantees that customer wallets never go into negative balance while preserving full auditability.
/// </summary>
public sealed class RecoveryOutstandingRecord
{
    private RecoveryOutstandingRecord() { } // EF Core

    public Guid Id { get; private set; }
    public Guid WalletId { get; private set; }
    public string SourceTransactionType { get; private set; } = string.Empty; // e.g. "CardRefund", "Chargeback", "Reversal"
    public string SourceReference { get; private set; } = string.Empty;
    public PaymentProvider Provider { get; private set; }
    public decimal AmountOwed { get; private set; }
    public decimal AmountRecovered { get; private set; }
    public Currency Currency { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public RecoveryStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public string? LastActionDetails { get; private set; }

    public static RecoveryOutstandingRecord Create(
        Guid walletId,
        string sourceTransactionType,
        string sourceReference,
        PaymentProvider provider,
        decimal amountOwed,
        Currency currency,
        string reason)
    {
        if (walletId == Guid.Empty)
            throw new ArgumentException("WalletId is required.", nameof(walletId));
        if (string.IsNullOrWhiteSpace(sourceTransactionType))
            throw new ArgumentException("SourceTransactionType is required.", nameof(sourceTransactionType));
        if (string.IsNullOrWhiteSpace(sourceReference))
            throw new ArgumentException("SourceReference is required.", nameof(sourceReference));
        if (amountOwed <= 0)
            throw new ArgumentException("AmountOwed must be positive.", nameof(amountOwed));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        currency.EnsureTransactionalV1();

        return new RecoveryOutstandingRecord
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            SourceTransactionType = sourceTransactionType.Trim(),
            SourceReference = sourceReference.Trim(),
            Provider = provider,
            AmountOwed = amountOwed,
            AmountRecovered = 0m,
            Currency = currency,
            Reason = reason.Trim(),
            Status = RecoveryStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void RecordRecovery(decimal amount, string actionDetails)
    {
        if (amount <= 0)
            throw new ArgumentException("Recovery amount must be positive.", nameof(amount));

        AmountRecovered += amount;
        LastActionDetails = actionDetails?.Trim();

        if (AmountRecovered >= AmountOwed)
        {
            Status = RecoveryStatus.FullyRecovered;
            ResolvedAtUtc = DateTime.UtcNow;
        }
        else
        {
            Status = RecoveryStatus.PartiallyRecovered;
        }
    }

    public void MarkWrittenOff(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Write-off reason is required.", nameof(reason));

        Status = RecoveryStatus.WrittenOff;
        ResolvedAtUtc = DateTime.UtcNow;
        LastActionDetails = $"Written off: {reason.Trim()}";
    }

    public void MarkDisputed(string reason)
    {
        Status = RecoveryStatus.Disputed;
        LastActionDetails = $"Disputed: {reason?.Trim()}";
    }
}
