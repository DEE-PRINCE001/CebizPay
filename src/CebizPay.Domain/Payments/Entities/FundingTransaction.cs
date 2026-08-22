using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Domain.Payments.Entities;

/// <summary>
/// Domain aggregate entity representing an inbound wallet funding transaction (Virtual Account Deposit or Card Payment).
/// Tracks the payment provider execution state independently of internal double-entry ledger transactions.
/// </summary>
public class FundingTransaction
{
    /// <summary>Unique funding transaction identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Target wallet identifier receiving the credited funds.</summary>
    public Guid WalletId { get; private set; }

    /// <summary>Linked virtual account identifier (if funded via dedicated virtual account).</summary>
    public Guid? VirtualAccountId { get; private set; }

    /// <summary>Linked central ledger transaction ID (authoritative double-entry record upon completion).</summary>
    public Guid? LedgerTransactionId { get; private set; }

    /// <summary>Payment service provider executing the funding.</summary>
    public PaymentProvider Provider { get; private set; }

    /// <summary>Unique provider transaction reference or checkout session reference.</summary>
    public string ProviderTransactionReference { get; private set; } = string.Empty;

    /// <summary>Payment funding channel (VirtualAccount or Card).</summary>
    public FundingChannel FundingChannel { get; private set; }

    /// <summary>Gross funding amount.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Funding currency (strictly transactional V1 currency).</summary>
    public Currency Currency { get; private set; }

    /// <summary>Current funding lifecycle status.</summary>
    public FundingTransactionStatus Status { get; private set; }

    /// <summary>Failure reason if the funding transaction failed.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Completion timestamp (UTC).</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Failure timestamp (UTC).</summary>
    public DateTime? FailedAtUtc { get; private set; }

    private FundingTransaction() { }

    /// <summary>
    /// Creates a new funding transaction in PENDING status.
    /// </summary>
    public static FundingTransaction Create(
        Guid walletId,
        Guid? virtualAccountId,
        PaymentProvider provider,
        string providerTransactionReference,
        FundingChannel fundingChannel,
        decimal amount,
        Currency currency)
    {
        if (walletId == Guid.Empty)
            throw new ArgumentException("WalletId cannot be empty.", nameof(walletId));

        if (amount <= 0)
            throw new ArgumentException("Funding amount must be positive.", nameof(amount));

        if (string.IsNullOrWhiteSpace(providerTransactionReference))
            throw new ArgumentException("Provider transaction reference cannot be empty.", nameof(providerTransactionReference));

        currency.EnsureTransactionalV1();

        return new FundingTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            VirtualAccountId = virtualAccountId,
            Provider = provider,
            ProviderTransactionReference = providerTransactionReference.Trim(),
            FundingChannel = fundingChannel,
            Amount = amount,
            Currency = currency,
            Status = FundingTransactionStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Marks the funding transaction as COMPLETED and links the central double-entry ledger transaction ID.
    /// </summary>
    public void MarkCompleted(Guid ledgerTransactionId)
    {
        if (Status == FundingTransactionStatus.Completed)
            return;

        if (ledgerTransactionId == Guid.Empty)
            throw new ArgumentException("Ledger transaction ID cannot be empty.", nameof(ledgerTransactionId));

        Status = FundingTransactionStatus.Completed;
        LedgerTransactionId = ledgerTransactionId;
        CompletedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the funding transaction as FAILED.
    /// </summary>
    public void MarkFailed(string reason)
    {
        if (Status == FundingTransactionStatus.Completed)
            throw new InvalidOperationException("Cannot mark a completed funding transaction as failed.");

        Status = FundingTransactionStatus.Failed;
        FailureReason = reason;
        FailedAtUtc = DateTime.UtcNow;
    }
}
