using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Vas.Enums;

namespace CebizPay.Domain.Vas.Entities;

/// <summary>
/// Domain aggregate root representing a Value-Added Service (VAS) purchase transaction (Airtime / Data).
/// Governs deterministic state transitions, audit trail, and failure/reversal tracking.
/// </summary>
public sealed class VasTransaction
{
    private VasTransaction()
    {
        // Required for EF Core
        Reference = string.Empty;
        UserId = string.Empty;
        PhoneNumber = string.Empty;
    }

    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Unique CebizPay VAS transaction reference (e.g., CBZVAS-...).</summary>
    public string Reference { get; private set; }

    /// <summary>Identifier of the user who initiated the transaction.</summary>
    public string UserId { get; private set; }

    /// <summary>Optional organization context identifier for corporate purchases.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Customer wallet debited for this purchase.</summary>
    public Guid WalletId { get; private set; }

    /// <summary>Identifier of the associated central ledger transaction.</summary>
    public Guid LedgerTransactionId { get; private set; }

    /// <summary>VAS product category (Airtime or Data).</summary>
    public VasType Type { get; private set; }

    /// <summary>Current lifecycle status of the transaction.</summary>
    public VasTransactionStatus Status { get; private set; }

    /// <summary>VAS provider gateway utilized.</summary>
    public VasProvider Provider { get; private set; }

    /// <summary>External gateway reference or transaction token returned by the provider.</summary>
    public string? ProviderReference { get; private set; }

    /// <summary>Recipient mobile phone number.</summary>
    public string PhoneNumber { get; private set; }

    /// <summary>Mobile network operator.</summary>
    public VasNetwork Network { get; private set; }

    /// <summary>Monetary purchase amount.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Currency of the purchase (NGN).</summary>
    public Currency Currency { get; private set; }

    /// <summary>Provider product or bundle code (for Data bundles).</summary>
    public string? ProductCode { get; private set; }

    /// <summary>Human-readable product/bundle name (e.g. "1.5GB 30-Day Data").</summary>
    public string? ProductName { get; private set; }

    /// <summary>Total dispatch attempts made for this transaction.</summary>
    public int AttemptCount { get; private set; }

    /// <summary>Gateway failure code if transaction failed or rejected.</summary>
    public string? FailureCode { get; private set; }

    /// <summary>Gateway failure message or reason description.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Timestamp when the transaction was created.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Timestamp when active provider dispatch processing began.</summary>
    public DateTime? ProcessingStartedAtUtc { get; private set; }

    /// <summary>Timestamp when fulfillment definitively completed.</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Timestamp when the transaction was financially reversed.</summary>
    public DateTime? ReversedAtUtc { get; private set; }

    /// <summary>
    /// Creates a new Airtime purchase transaction in <see cref="VasTransactionStatus.Pending"/> status.
    /// </summary>
    public static VasTransaction CreateAirtime(
        string reference,
        string userId,
        Guid? organizationId,
        Guid walletId,
        Guid ledgerTransactionId,
        string phoneNumber,
        VasNetwork network,
        decimal amount,
        Currency currency = Currency.NGN,
        VasProvider provider = VasProvider.VtuGate)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Transaction reference is required.", nameof(reference));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (walletId == Guid.Empty)
            throw new ArgumentException("WalletId cannot be empty.", nameof(walletId));
        if (ledgerTransactionId == Guid.Empty)
            throw new ArgumentException("LedgerTransactionId cannot be empty.", nameof(ledgerTransactionId));
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("PhoneNumber is required.", nameof(phoneNumber));
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));

        return new VasTransaction
        {
            Id = Guid.NewGuid(),
            Reference = reference.Trim(),
            UserId = userId.Trim(),
            OrganizationId = organizationId,
            WalletId = walletId,
            LedgerTransactionId = ledgerTransactionId,
            Type = VasType.Airtime,
            Status = VasTransactionStatus.Pending,
            Provider = provider,
            PhoneNumber = phoneNumber.Trim(),
            Network = network,
            Amount = amount,
            Currency = currency,
            ProductCode = null,
            ProductName = "Airtime Top-Up",
            AttemptCount = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new Data bundle purchase transaction in <see cref="VasTransactionStatus.Pending"/> status.
    /// </summary>
    public static VasTransaction CreateData(
        string reference,
        string userId,
        Guid? organizationId,
        Guid walletId,
        Guid ledgerTransactionId,
        string phoneNumber,
        VasNetwork network,
        string productCode,
        string? productName,
        decimal amount,
        Currency currency = Currency.NGN,
        VasProvider provider = VasProvider.VtuGate)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Transaction reference is required.", nameof(reference));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (walletId == Guid.Empty)
            throw new ArgumentException("WalletId cannot be empty.", nameof(walletId));
        if (ledgerTransactionId == Guid.Empty)
            throw new ArgumentException("LedgerTransactionId cannot be empty.", nameof(ledgerTransactionId));
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("PhoneNumber is required.", nameof(phoneNumber));
        if (string.IsNullOrWhiteSpace(productCode))
            throw new ArgumentException("ProductCode is required for data bundles.", nameof(productCode));
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));

        return new VasTransaction
        {
            Id = Guid.NewGuid(),
            Reference = reference.Trim(),
            UserId = userId.Trim(),
            OrganizationId = organizationId,
            WalletId = walletId,
            LedgerTransactionId = ledgerTransactionId,
            Type = VasType.Data,
            Status = VasTransactionStatus.Pending,
            Provider = provider,
            PhoneNumber = phoneNumber.Trim(),
            Network = network,
            Amount = amount,
            Currency = currency,
            ProductCode = productCode.Trim(),
            ProductName = string.IsNullOrWhiteSpace(productName) ? $"Data Bundle ({productCode})" : productName.Trim(),
            AttemptCount = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Transitions status to <see cref="VasTransactionStatus.Processing"/>.
    /// </summary>
    public void MarkProcessing()
    {
        if (Status == VasTransactionStatus.Succeeded)
            throw new InvalidOperationException("Cannot transition a succeeded VAS transaction to processing.");
        if (Status == VasTransactionStatus.Reversed)
            throw new InvalidOperationException("Cannot transition a reversed VAS transaction to processing.");

        Status = VasTransactionStatus.Processing;
        ProcessingStartedAtUtc ??= DateTime.UtcNow;
        AttemptCount++;
    }

    /// <summary>
    /// Transitions status to <see cref="VasTransactionStatus.Succeeded"/>.
    /// </summary>
    public void MarkSucceeded(string providerReference)
    {
        if (Status == VasTransactionStatus.Succeeded)
            return;
        if (Status == VasTransactionStatus.Reversed)
            throw new InvalidOperationException("Cannot mark a reversed VAS transaction as succeeded.");

        Status = VasTransactionStatus.Succeeded;
        ProviderReference = string.IsNullOrWhiteSpace(providerReference) ? ProviderReference : providerReference.Trim();
        CompletedAtUtc = DateTime.UtcNow;
        FailureCode = null;
        FailureReason = null;
    }

    /// <summary>
    /// Transitions status to <see cref="VasTransactionStatus.Failed"/>.
    /// </summary>
    public void MarkFailed(string? failureCode, string failureReason)
    {
        if (Status == VasTransactionStatus.Succeeded)
            throw new InvalidOperationException("Cannot fail an already succeeded VAS transaction.");

        Status = VasTransactionStatus.Failed;
        FailureCode = failureCode?.Trim();
        FailureReason = string.IsNullOrWhiteSpace(failureReason) ? "VAS transaction failed." : failureReason.Trim();
        CompletedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Transitions status to <see cref="VasTransactionStatus.Unknown"/> when a timeout or indeterminate outcome occurs.
    /// </summary>
    public void MarkUnknown(string reason)
    {
        if (Status == VasTransactionStatus.Succeeded)
            throw new InvalidOperationException("Cannot transition a succeeded VAS transaction to unknown.");
        if (Status == VasTransactionStatus.Reversed)
            throw new InvalidOperationException("Cannot transition a reversed VAS transaction to unknown.");

        Status = VasTransactionStatus.Unknown;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Fulfillment outcome is currently unknown/pending." : reason.Trim();
    }

    /// <summary>
    /// Transitions status to <see cref="VasTransactionStatus.Reversed"/> following definitive failure and financial refund.
    /// </summary>
    public void MarkReversed(string reason)
    {
        if (Status == VasTransactionStatus.Succeeded)
            throw new InvalidOperationException("Cannot reverse a succeeded VAS transaction.");
        if (Status == VasTransactionStatus.Reversed)
            return;

        Status = VasTransactionStatus.Reversed;
        ReversedAtUtc = DateTime.UtcNow;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? FailureReason : reason.Trim();
    }

    /// <summary>
    /// Returns a masked representation of the recipient phone number (e.g. "0803***4567").
    /// </summary>
    public string GetMaskedPhoneNumber()
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber) || PhoneNumber.Length < 7)
            return PhoneNumber;

        var clean = PhoneNumber.Trim();
        var prefix = clean[..4];
        var suffix = clean[^4..];
        return $"{prefix}***{suffix}";
    }
}
