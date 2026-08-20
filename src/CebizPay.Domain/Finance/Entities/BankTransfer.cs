using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Finance.Entities;

/// <summary>
/// Domain entity representing an outbound bank transfer financial operation.
///
/// Financial Model (Option A):
///   1. Immediately debits sender wallet into a Platform Clearing ledger account.
///   2. Created in Status = PENDING.
///   3. Follows locked state lifecycle (PENDING -> PROCESSING -> COMPLETED | FAILED | UNKNOWN).
///   4. Definitive failure triggers an atomic reversal ledger transaction.
/// </summary>
public class BankTransfer
{
    /// <summary>Unique bank transfer identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Linked central ledger transaction ID.</summary>
    public Guid LedgerTransactionId { get; private set; }

    /// <summary>Source wallet identifier debited for this transfer.</summary>
    public Guid SenderWalletId { get; private set; }

    /// <summary>Destination bank institution code (e.g. "058", "044", "011").</summary>
    public string DestinationBankCode { get; private set; } = string.Empty;

    /// <summary>Destination bank account number.</summary>
    public string DestinationAccountNumber { get; private set; } = string.Empty;

    /// <summary>Resolved beneficiary account name (optional in V1).</summary>
    public string? DestinationAccountName { get; private set; }

    /// <summary>Principal transfer amount.</summary>
    public decimal Amount { get; private set; }

    /// <summary>V1 transactional currency (NGN, INTERNATIONAL_NGN, USDT).</summary>
    public Currency Currency { get; private set; }

    /// <summary>Fee amount calculated and debited for this transfer.</summary>
    public decimal FeeAmount { get; private set; }

    /// <summary>Total debited from sender (Amount + FeeAmount).</summary>
    public decimal TotalDebited { get; private set; }

    /// <summary>Applied fee policy ID, if any.</summary>
    public Guid? FeePolicyId { get; private set; }

    /// <summary>Applied fee policy version number for historical traceability.</summary>
    public int? FeePolicyVersion { get; private set; }

    /// <summary>Current status in the bank transfer lifecycle.</summary>
    public BankTransferStatus Status { get; private set; }

    /// <summary>Unique business reference string (e.g. "CBZBT-XXXXX").</summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last state update timestamp (UTC).</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Timestamp when external settlement completed successfully.</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Timestamp when transfer definitively failed.</summary>
    public DateTime? FailedAtUtc { get; private set; }

    /// <summary>Reason for definitive failure, if applicable.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>External provider transaction reference (reserved for Phase 3 integration).</summary>
    public string? ProviderReference { get; private set; }

    private BankTransfer() { } // EF Core

    /// <summary>
    /// Factory method for creating a new pending bank transfer.
    /// </summary>
    public static BankTransfer CreatePending(
        Guid ledgerTransactionId,
        Guid senderWalletId,
        string destinationBankCode,
        string destinationAccountNumber,
        string? destinationAccountName,
        decimal amount,
        Currency currency,
        decimal feeAmount,
        Guid? feePolicyId,
        int? feePolicyVersion,
        string reference)
    {
        if (ledgerTransactionId == Guid.Empty)
            throw new ArgumentException("LedgerTransactionId is required.", nameof(ledgerTransactionId));
        if (senderWalletId == Guid.Empty)
            throw new ArgumentException("SenderWalletId is required.", nameof(senderWalletId));
        if (string.IsNullOrWhiteSpace(destinationBankCode))
            throw new ArgumentException("DestinationBankCode is required.", nameof(destinationBankCode));
        if (string.IsNullOrWhiteSpace(destinationAccountNumber))
            throw new ArgumentException("DestinationAccountNumber is required.", nameof(destinationAccountNumber));
        if (amount <= 0)
            throw new ArgumentException("Transfer amount must be positive.", nameof(amount));
        if (feeAmount < 0)
            throw new ArgumentException("Fee amount cannot be negative.", nameof(feeAmount));
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Reference is required.", nameof(reference));
        if (!currency.IsTransactionalV1())
            throw new ArgumentException($"Currency {currency} is not supported for bank transfers.", nameof(currency));

        var now = DateTime.UtcNow;

        return new BankTransfer
        {
            Id = Guid.NewGuid(),
            LedgerTransactionId = ledgerTransactionId,
            SenderWalletId = senderWalletId,
            DestinationBankCode = destinationBankCode.Trim(),
            DestinationAccountNumber = destinationAccountNumber.Trim(),
            DestinationAccountName = string.IsNullOrWhiteSpace(destinationAccountName) ? null : destinationAccountName.Trim(),
            Amount = amount,
            Currency = currency,
            FeeAmount = feeAmount,
            TotalDebited = amount + feeAmount,
            FeePolicyId = feePolicyId,
            FeePolicyVersion = feePolicyVersion,
            Status = BankTransferStatus.Pending,
            Reference = reference.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    /// <summary>
    /// Transitions status from PENDING to PROCESSING.
    /// </summary>
    public void MarkProcessing()
    {
        if (Status != BankTransferStatus.Pending)
            throw new InvalidOperationException($"Cannot transition BankTransfer to Processing from status '{Status}'.");

        Status = BankTransferStatus.Processing;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Transitions status to COMPLETED upon confirmed external success.
    /// Allowed from PENDING, PROCESSING, or UNKNOWN.
    /// </summary>
    public void MarkCompleted(DateTime? completedAtUtc = null, string? providerReference = null)
    {
        if (Status != BankTransferStatus.Pending &&
            Status != BankTransferStatus.Processing &&
            Status != BankTransferStatus.Unknown)
        {
            throw new InvalidOperationException($"Cannot transition BankTransfer to Completed from terminal/invalid status '{Status}'.");
        }

        Status = BankTransferStatus.Completed;
        CompletedAtUtc = completedAtUtc ?? DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(providerReference))
        {
            ProviderReference = providerReference.Trim();
        }
    }

    /// <summary>
    /// Transitions status to FAILED upon definitive external failure.
    /// Allowed from PENDING, PROCESSING, or UNKNOWN.
    /// </summary>
    public void MarkFailed(string reason, DateTime? failedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Failure reason is required.", nameof(reason));

        if (Status != BankTransferStatus.Pending &&
            Status != BankTransferStatus.Processing &&
            Status != BankTransferStatus.Unknown)
        {
            throw new InvalidOperationException($"Cannot transition BankTransfer to Failed from terminal/invalid status '{Status}'.");
        }

        Status = BankTransferStatus.Failed;
        FailureReason = reason.Trim();
        FailedAtUtc = failedAtUtc ?? DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Transitions status to UNKNOWN when external state cannot be determined (e.g. timeout).
    /// Allowed from PENDING or PROCESSING.
    /// </summary>
    public void MarkUnknown(string? reason = null)
    {
        if (Status != BankTransferStatus.Pending && Status != BankTransferStatus.Processing)
            throw new InvalidOperationException($"Cannot transition BankTransfer to Unknown from status '{Status}'.");

        Status = BankTransferStatus.Unknown;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            FailureReason = reason.Trim();
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns the destination account number masked for safe display (e.g. "******1234").
    /// </summary>
    public string GetMaskedAccountNumber()
    {
        if (string.IsNullOrWhiteSpace(DestinationAccountNumber))
            return string.Empty;

        var clean = DestinationAccountNumber.Trim();
        if (clean.Length <= 4)
            return new string('*', clean.Length);

        return new string('*', clean.Length - 4) + clean[^4..];
    }
}
