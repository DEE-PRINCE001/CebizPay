using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Domain.Payments.Entities;

/// <summary>
/// Domain entity representing a tokenized, reusable payment card.
/// Strictly stores only provider-safe token references, brand, and truncated last4 metadata.
/// Never receives, stores, or handles raw PAN, CVV, PIN, or payment credentials.
/// </summary>
public class SavedCard
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Owning individual user ID.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>Associated customer wallet ID.</summary>
    public Guid WalletId { get; private set; }

    /// <summary>Payment service provider issuing the token (e.g. Flutterwave or Paystack).</summary>
    public PaymentProvider Provider { get; private set; }

    /// <summary>Provider-specific customer reference or code if applicable.</summary>
    public string? ProviderCustomerReference { get; private set; }

    /// <summary>Opaque provider payment method token (e.g. Flutterwave card token or Paystack authorization code).</summary>
    public string ProviderToken { get; private set; } = string.Empty;

    /// <summary>Last 4 digits of the masked card number.</summary>
    public string Last4 { get; private set; } = string.Empty;

    /// <summary>Card network brand (e.g. Visa, Mastercard, Verve).</summary>
    public string Brand { get; private set; } = string.Empty;

    /// <summary>Two-digit expiration month string if available.</summary>
    public string? ExpiryMonth { get; private set; }

    /// <summary>Expiration year string (e.g. 2028) if available.</summary>
    public string? ExpiryYear { get; private set; }

    /// <summary>Cardholder name if returned by provider.</summary>
    public string? CardHolderName { get; private set; }

    /// <summary>Lifecycle status of the saved card token.</summary>
    public SavedCardStatus Status { get; private set; }

    /// <summary>Flag indicating whether this is the primary/default card for the user.</summary>
    public bool IsDefault { get; private set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last updated timestamp (UTC).</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    private SavedCard() { } // EF Core

    /// <summary>
    /// Creates a new tokenized saved card with strict security invariant validation.
    /// </summary>
    public static SavedCard Create(
        string userId,
        Guid walletId,
        PaymentProvider provider,
        string providerToken,
        string last4,
        string brand,
        string? expiryMonth = null,
        string? expiryYear = null,
        string? cardHolderName = null,
        string? providerCustomerReference = null,
        bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (walletId == Guid.Empty)
            throw new ArgumentException("WalletId is required.", nameof(walletId));
        if (string.IsNullOrWhiteSpace(providerToken))
            throw new ArgumentException("ProviderToken is required.", nameof(providerToken));
        if (string.IsNullOrWhiteSpace(last4))
            throw new ArgumentException("Last4 is required.", nameof(last4));

        var cleanLast4 = last4.Trim();
        if (cleanLast4.Length != 4 || !cleanLast4.All(char.IsDigit))
            throw new ArgumentException("Last4 must be exactly 4 digits.", nameof(last4));

        return new SavedCard
        {
            Id = Guid.NewGuid(),
            UserId = userId.Trim(),
            WalletId = walletId,
            Provider = provider,
            ProviderToken = providerToken.Trim(),
            Last4 = cleanLast4,
            Brand = string.IsNullOrWhiteSpace(brand) ? "Unknown" : brand.Trim(),
            ExpiryMonth = expiryMonth?.Trim(),
            ExpiryYear = expiryYear?.Trim(),
            CardHolderName = cardHolderName?.Trim(),
            ProviderCustomerReference = providerCustomerReference?.Trim(),
            Status = SavedCardStatus.Active,
            IsDefault = isDefault,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Sets this card as default or non-default.
    /// </summary>
    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Revokes the saved card, preventing any future charges.
    /// </summary>
    public void Revoke()
    {
        Status = SavedCardStatus.Revoked;
        IsDefault = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the saved card token as invalid due to provider rejection.
    /// </summary>
    public void MarkInvalid()
    {
        Status = SavedCardStatus.Invalid;
        IsDefault = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the card as expired.
    /// </summary>
    public void MarkExpired()
    {
        Status = SavedCardStatus.Expired;
        IsDefault = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
