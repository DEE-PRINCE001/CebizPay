using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Domain.Payments.Entities;

/// <summary>
/// Domain aggregate entity representing an external payment provider execution attempt.
///
/// Distinct from the authoritative CebizPay Financial Transaction:
/// A single CebizPay transaction (e.g. BankTransfer / Payout) can have one or more sequential
/// PaymentAttempts across supported providers (Flutterwave, Paystack).
///
/// Invariants:
///   - Provider references are unique per provider.
///   - Attempt numbers are strictly sequential per CebizPay transaction (Attempt #1, #2...).
///   - Controlled state transitions:
///       Created -> Processing | Cancelled
///       Processing -> Succeeded | Failed | Unknown
///       Unknown -> Succeeded | Failed | Cancelled
///   - Raw secrets / credentials are never stored.
/// </summary>
public class PaymentAttempt
{
    /// <summary>Unique payment attempt identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Linked central ledger transaction ID (authoritative CebizPay financial transaction).</summary>
    public Guid LedgerTransactionId { get; private set; }

    /// <summary>External payment service provider for this attempt.</summary>
    public PaymentProvider Provider { get; private set; }

    /// <summary>1-based attempt sequence number for the parent financial transaction.</summary>
    public int AttemptNumber { get; private set; }

    /// <summary>Current lifecycle status of this attempt.</summary>
    public PaymentAttemptStatus Status { get; private set; }

    /// <summary>Unique request reference sent to the provider (e.g. "CBZPA-XXXXX").</summary>
    public string RequestReference { get; private set; } = string.Empty;

    /// <summary>External provider transaction/session reference (e.g. "FLW-12345", "pstk_abc").</summary>
    public string? ProviderReference { get; private set; }

    /// <summary>Attempt monetary amount.</summary>
    public decimal Amount { get; private set; }

    /// <summary>V1 transactional currency (NGN, INTERNATIONAL_NGN, USDT).</summary>
    public Currency Currency { get; private set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Timestamp when attempt execution started with external provider (UTC).</summary>
    public DateTime? StartedAtUtc { get; private set; }

    /// <summary>Timestamp when attempt reached a terminal or settled state (UTC).</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Provider-specific error / failure code, if failed.</summary>
    public string? FailureCode { get; private set; }

    /// <summary>Detailed failure or cancellation reason, if applicable.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Safe sanitized metadata / response reference (no secrets, passwords, PINs, or raw PANs).</summary>
    public string? SafeMetadata { get; private set; }

    private PaymentAttempt() { } // EF Core

    /// <summary>
    /// Factory method for creating a new payment attempt in CREATED status.
    /// </summary>
    public static PaymentAttempt Create(
        Guid ledgerTransactionId,
        PaymentProvider provider,
        int attemptNumber,
        string requestReference,
        decimal amount,
        Currency currency,
        string? safeMetadata = null)
    {
        if (ledgerTransactionId == Guid.Empty)
            throw new ArgumentException("LedgerTransactionId is required.", nameof(ledgerTransactionId));
        if (attemptNumber < 1)
            throw new ArgumentException("AttemptNumber must be greater than or equal to 1.", nameof(attemptNumber));
        if (string.IsNullOrWhiteSpace(requestReference))
            throw new ArgumentException("RequestReference is required.", nameof(requestReference));
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));
        if (!currency.IsTransactionalV1())
            throw new ArgumentException($"Currency '{currency}' is not supported for payment attempts.", nameof(currency));

        return new PaymentAttempt
        {
            Id = Guid.NewGuid(),
            LedgerTransactionId = ledgerTransactionId,
            Provider = provider,
            AttemptNumber = attemptNumber,
            Status = PaymentAttemptStatus.Created,
            RequestReference = requestReference.Trim(),
            Amount = amount,
            Currency = currency,
            SafeMetadata = string.IsNullOrWhiteSpace(safeMetadata) ? null : safeMetadata.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Transitions attempt status from CREATED to PROCESSING when external execution starts.
    /// </summary>
    public void MarkProcessing(DateTime? startedAtUtc = null)
    {
        if (Status != PaymentAttemptStatus.Created)
            throw new InvalidOperationException($"Cannot transition PaymentAttempt to Processing from status '{Status}'.");

        Status = PaymentAttemptStatus.Processing;
        StartedAtUtc = startedAtUtc ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Transitions attempt status to SUCCEEDED upon confirmed provider success.
    /// Allowed only from PROCESSING or UNKNOWN.
    /// </summary>
    public void MarkSucceeded(string providerReference, DateTime? completedAtUtc = null, string? safeMetadata = null)
    {
        if (string.IsNullOrWhiteSpace(providerReference))
            throw new ArgumentException("ProviderReference is required when marking an attempt as succeeded.", nameof(providerReference));

        if (Status != PaymentAttemptStatus.Processing && Status != PaymentAttemptStatus.Unknown)
            throw new InvalidOperationException($"Cannot transition PaymentAttempt to Succeeded from invalid status '{Status}'.");

        Status = PaymentAttemptStatus.Succeeded;
        ProviderReference = providerReference.Trim();
        CompletedAtUtc = completedAtUtc ?? DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(safeMetadata))
        {
            SafeMetadata = safeMetadata.Trim();
        }
    }

    /// <summary>
    /// Transitions attempt status to FAILED upon definitive provider failure or rejection.
    /// Allowed only from PROCESSING or UNKNOWN.
    /// </summary>
    public void MarkFailed(string? failureCode, string failureReason, DateTime? completedAtUtc = null, string? safeMetadata = null)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("FailureReason is required when marking an attempt as failed.", nameof(failureReason));

        if (Status != PaymentAttemptStatus.Processing && Status != PaymentAttemptStatus.Unknown)
            throw new InvalidOperationException($"Cannot transition PaymentAttempt to Failed from invalid status '{Status}'.");

        Status = PaymentAttemptStatus.Failed;
        FailureCode = string.IsNullOrWhiteSpace(failureCode) ? null : failureCode.Trim();
        FailureReason = failureReason.Trim();
        CompletedAtUtc = completedAtUtc ?? DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(safeMetadata))
        {
            SafeMetadata = safeMetadata.Trim();
        }
    }

    /// <summary>
    /// Transitions attempt status to UNKNOWN when external outcome cannot be determined (e.g. timeout / network partition).
    /// Allowed only from PROCESSING.
    /// </summary>
    public void MarkUnknown(string? reason = null, string? safeMetadata = null)
    {
        if (Status != PaymentAttemptStatus.Processing)
            throw new InvalidOperationException($"Cannot transition PaymentAttempt to Unknown from status '{Status}'.");

        Status = PaymentAttemptStatus.Unknown;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            FailureReason = reason.Trim();
        }

        if (!string.IsNullOrWhiteSpace(safeMetadata))
        {
            SafeMetadata = safeMetadata.Trim();
        }
    }

    /// <summary>
    /// Transitions attempt status to CANCELLED before dispatch or after unresolved unknown state.
    /// Allowed only from CREATED or UNKNOWN.
    /// </summary>
    public void MarkCancelled(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Cancellation reason is required.", nameof(reason));

        if (Status != PaymentAttemptStatus.Created && Status != PaymentAttemptStatus.Unknown)
            throw new InvalidOperationException($"Cannot transition PaymentAttempt to Cancelled from status '{Status}'.");

        Status = PaymentAttemptStatus.Cancelled;
        FailureReason = reason.Trim();
        CompletedAtUtc = DateTime.UtcNow;
    }
}
