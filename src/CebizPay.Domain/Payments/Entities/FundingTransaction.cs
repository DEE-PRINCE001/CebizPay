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

    /// <summary>Linked virtual account identifier (if funded via legacy dedicated virtual account).</summary>
    public Guid? VirtualAccountId { get; private set; }

    /// <summary>Linked external funding account identifier (if funded via ExternalFundingAccount rail, e.g. Monnify).</summary>
    public Guid? ExternalFundingAccountId { get; private set; }

    /// <summary>Linked central ledger transaction ID (authoritative double-entry record upon completion).</summary>
    public Guid? LedgerTransactionId { get; private set; }

    /// <summary>Payment service provider executing the funding.</summary>
    public PaymentProvider Provider { get; private set; }

    /// <summary>Unique provider transaction reference or checkout session reference.</summary>
    public string ProviderTransactionReference { get; private set; } = string.Empty;

    /// <summary>Unique provider webhook event reference / identifier if available.</summary>
    public string? ProviderEventReference { get; private set; }

    /// <summary>Payment funding channel (VirtualAccount or Card).</summary>
    public FundingChannel FundingChannel { get; private set; }

    /// <summary>Gross funding amount received from external provider.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Platform fee charged according to the active PlatformFeePolicy.</summary>
    public decimal FeeAmount { get; private set; }

    /// <summary>Net amount credited to the target wallet.</summary>
    public decimal NetCreditedAmount { get; private set; }

    /// <summary>Provider fee incurred from external provider where available.</summary>
    public decimal ProviderFeeAmount { get; private set; }

    /// <summary>ID of the applied PlatformFeePolicy.</summary>
    public Guid? FeePolicyId { get; private set; }

    /// <summary>Version of the applied PlatformFeePolicy.</summary>
    public int? FeePolicyVersion { get; private set; }

    /// <summary>Settlement fee bearer allocation model.</summary>
    public FeeBearer? FeeBearer { get; private set; }

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
            FeeAmount = 0m,
            NetCreditedAmount = amount,
            ProviderFeeAmount = 0m,
            Currency = currency,
            Status = FundingTransactionStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new funding transaction attached to an ExternalFundingAccount with full fee policy tracking.
    /// </summary>
    public static FundingTransaction CreateWithExternalAccount(
        Guid walletId,
        Guid externalFundingAccountId,
        PaymentProvider provider,
        string providerTransactionReference,
        string? providerEventReference,
        FundingChannel fundingChannel,
        decimal grossAmount,
        decimal feeAmount,
        decimal netCreditedAmount,
        decimal providerFeeAmount,
        Guid? feePolicyId,
        int? feePolicyVersion,
        FeeBearer? feeBearer,
        Currency currency)
    {
        if (walletId == Guid.Empty)
            throw new ArgumentException("WalletId cannot be empty.", nameof(walletId));
        if (externalFundingAccountId == Guid.Empty)
            throw new ArgumentException("ExternalFundingAccountId cannot be empty.", nameof(externalFundingAccountId));
        if (grossAmount <= 0)
            throw new ArgumentException("Gross funding amount must be positive.", nameof(grossAmount));
        if (feeAmount < 0)
            throw new ArgumentException("Fee amount cannot be negative.", nameof(feeAmount));
        if (netCreditedAmount < 0)
            throw new ArgumentException("Net credited amount cannot be negative.", nameof(netCreditedAmount));
        if (string.IsNullOrWhiteSpace(providerTransactionReference))
            throw new ArgumentException("Provider transaction reference cannot be empty.", nameof(providerTransactionReference));

        currency.EnsureTransactionalV1();

        return new FundingTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            ExternalFundingAccountId = externalFundingAccountId,
            Provider = provider,
            ProviderTransactionReference = providerTransactionReference.Trim(),
            ProviderEventReference = providerEventReference?.Trim(),
            FundingChannel = fundingChannel,
            Amount = grossAmount,
            FeeAmount = feeAmount,
            NetCreditedAmount = netCreditedAmount,
            ProviderFeeAmount = providerFeeAmount,
            FeePolicyId = feePolicyId,
            FeePolicyVersion = feePolicyVersion,
            FeeBearer = feeBearer,
            Currency = currency,
            Status = FundingTransactionStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Transitions the funding transaction status to PROCESSING.
    /// </summary>
    public void MarkProcessing()
    {
        if (Status == FundingTransactionStatus.Completed)
            return;

        Status = FundingTransactionStatus.Processing;
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

    /// <summary>
    /// Marks the funding transaction as UNKNOWN (pending reconciliation).
    /// </summary>
    public void MarkUnknown(string reason)
    {
        if (Status == FundingTransactionStatus.Completed)
            return;

        Status = FundingTransactionStatus.Unknown;
        FailureReason = reason;
    }

    /// <summary>
    /// Marks the funding transaction as REVERSED.
    /// </summary>
    public void MarkReversed(string reason)
    {
        Status = FundingTransactionStatus.Reversed;
        FailureReason = reason;
    }
}
