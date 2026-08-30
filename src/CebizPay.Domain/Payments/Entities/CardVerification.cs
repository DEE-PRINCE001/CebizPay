using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Domain.Payments.Entities;

/// <summary>
/// Domain entity representing a card authentication / zero-auth or micro-charge verification workflow.
/// </summary>
public class CardVerification
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Target user ID.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>Target customer wallet ID.</summary>
    public Guid WalletId { get; private set; }

    /// <summary>Payment service provider performing verification.</summary>
    public PaymentProvider Provider { get; private set; }

    /// <summary>Internal verification transaction reference.</summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>External provider transaction / session reference.</summary>
    public string? ProviderReference { get; private set; }

    /// <summary>Linked saved card ID once tokenized.</summary>
    public Guid? SavedCardId { get; private set; }

    /// <summary>Verification amount (0 for zero-auth or nominal micro-charge amount e.g. 50 NGN).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Verification currency.</summary>
    public Currency Currency { get; private set; }

    /// <summary>Current verification lifecycle status.</summary>
    public CardVerificationStatus Status { get; private set; }

    /// <summary>Failure reason if verification failed.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Completion timestamp (UTC).</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    private CardVerification() { } // EF Core

    /// <summary>
    /// Creates a new card verification session.
    /// </summary>
    public static CardVerification Create(
        string userId,
        Guid walletId,
        PaymentProvider provider,
        string reference,
        decimal amount = 0m,
        Currency currency = Currency.NGN)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (walletId == Guid.Empty)
            throw new ArgumentException("WalletId is required.", nameof(walletId));
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Reference is required.", nameof(reference));
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        currency.EnsureTransactionalV1();

        return new CardVerification
        {
            Id = Guid.NewGuid(),
            UserId = userId.Trim(),
            WalletId = walletId,
            Provider = provider,
            Reference = reference.Trim(),
            Amount = amount,
            Currency = currency,
            Status = CardVerificationStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Marks the verification as successful and links the created saved card token.
    /// </summary>
    public void MarkVerified(Guid savedCardId, string? providerReference)
    {
        if (savedCardId == Guid.Empty)
            throw new ArgumentException("SavedCardId is required.", nameof(savedCardId));

        Status = CardVerificationStatus.Verified;
        SavedCardId = savedCardId;
        ProviderReference = providerReference?.Trim();
        CompletedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the verification as failed.
    /// </summary>
    public void MarkFailed(string failureReason)
    {
        Status = CardVerificationStatus.Failed;
        FailureReason = failureReason;
        CompletedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the micro-charge verification as refunded.
    /// </summary>
    public void MarkRefunded()
    {
        Status = CardVerificationStatus.Refunded;
    }
}
