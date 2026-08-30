using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Domain.Payments.Entities;

/// <summary>
/// Domain entity representing a card payment refund operation.
/// Tracks provider refund execution, central ledger reversal, and potential recovery states.
/// </summary>
public class CardRefund
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Associated parent funding transaction ID.</summary>
    public Guid FundingTransactionId { get; private set; }

    /// <summary>Target customer wallet ID.</summary>
    public Guid WalletId { get; private set; }

    /// <summary>Payment provider that processed the original card payment.</summary>
    public PaymentProvider Provider { get; private set; }

    /// <summary>Durable internal CebizPay refund reference.</summary>
    public string RefundReference { get; private set; } = string.Empty;

    /// <summary>External provider refund reference if returned.</summary>
    public string? ProviderRefundReference { get; private set; }

    /// <summary>Idempotency key for deterministic replay protection.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>Refund monetary amount.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Refund currency.</summary>
    public Currency Currency { get; private set; }

    /// <summary>Current refund lifecycle status.</summary>
    public CardRefundStatus Status { get; private set; }

    /// <summary>Reason for the refund.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Linked central ledger transaction ID for the double-entry reversal.</summary>
    public Guid? LedgerTransactionId { get; private set; }

    /// <summary>Failure reason if refund was rejected.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Completion or settlement timestamp (UTC).</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    private CardRefund() { } // EF Core

    /// <summary>
    /// Creates a new card refund request.
    /// </summary>
    public static CardRefund Create(
        Guid fundingTransactionId,
        Guid walletId,
        PaymentProvider provider,
        string refundReference,
        string idempotencyKey,
        decimal amount,
        Currency currency,
        string reason)
    {
        if (fundingTransactionId == Guid.Empty)
            throw new ArgumentException("FundingTransactionId is required.", nameof(fundingTransactionId));
        if (walletId == Guid.Empty)
            throw new ArgumentException("WalletId is required.", nameof(walletId));
        if (string.IsNullOrWhiteSpace(refundReference))
            throw new ArgumentException("RefundReference is required.", nameof(refundReference));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));
        if (amount <= 0)
            throw new ArgumentException("Refund amount must be positive.", nameof(amount));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        currency.EnsureTransactionalV1();

        return new CardRefund
        {
            Id = Guid.NewGuid(),
            FundingTransactionId = fundingTransactionId,
            WalletId = walletId,
            Provider = provider,
            RefundReference = refundReference.Trim(),
            IdempotencyKey = idempotencyKey.Trim(),
            Amount = amount,
            Currency = currency,
            Reason = reason.Trim(),
            Status = CardRefundStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Marks the refund as successfully executed by provider and settled in the ledger.
    /// </summary>
    public void MarkSucceeded(string? providerRefundReference, Guid ledgerTransactionId)
    {
        if (ledgerTransactionId == Guid.Empty)
            throw new ArgumentException("LedgerTransactionId is required.", nameof(ledgerTransactionId));

        Status = CardRefundStatus.Succeeded;
        ProviderRefundReference = providerRefundReference?.Trim();
        LedgerTransactionId = ledgerTransactionId;
        CompletedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the refund as rejected or failed.
    /// </summary>
    public void MarkFailed(string failureReason)
    {
        Status = CardRefundStatus.Failed;
        FailureReason = failureReason;
        CompletedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the refund as provider-executed but customer wallet balance was insufficient for immediate full reversal.
    /// </summary>
    public void MarkRecoveryOutstanding(string reason)
    {
        Status = CardRefundStatus.RecoveryOutstanding;
        FailureReason = reason;
        CompletedAtUtc = DateTime.UtcNow;
    }
}
