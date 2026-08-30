using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Domain.Finance.Entities;

/// <summary>
/// Domain aggregate entity representing an external funding account attached directly to a <see cref="Wallet"/>.
/// Allows multiple external funding rails (Monnify, future BaaS, future CebizPay MFB core-banking)
/// per wallet without altering Wallet or Ledger financial models.
/// Invariant: ExternalFundingAccount is provider-neutral and holds no financial balance.
/// </summary>
public class ExternalFundingAccount
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Owning wallet identifier.</summary>
    public Guid WalletId { get; private set; }

    /// <summary>Owning wallet navigation property.</summary>
    public Wallet? Wallet { get; private set; }

    /// <summary>Payment / banking provider servicing this external account.</summary>
    public PaymentProvider Provider { get; private set; }

    /// <summary>Provider-specific customer reference/code (e.g. customer code or BVN ref).</summary>
    public string? ProviderCustomerReference { get; private set; }

    /// <summary>Provider-specific account reference/token (e.g. account reference or order ID).</summary>
    public string? ProviderAccountReference { get; private set; }

    /// <summary>External NUBAN or bank account number.</summary>
    public string AccountNumber { get; private set; } = string.Empty;

    /// <summary>Account holder beneficiary name registered with the partner bank.</summary>
    public string AccountName { get; private set; } = string.Empty;

    /// <summary>Partner bank routing / institution code.</summary>
    public string BankCode { get; private set; } = string.Empty;

    /// <summary>Partner bank institution name.</summary>
    public string BankName { get; private set; } = string.Empty;

    /// <summary>Transactional currency for this funding account.</summary>
    public Currency Currency { get; private set; }

    /// <summary>Lifecycle status of this funding account.</summary>
    public ExternalFundingAccountStatus Status { get; private set; }

    /// <summary>Whether this is the primary external funding account for the parent wallet.</summary>
    public bool IsPrimary { get; private set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last update timestamp (UTC).</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    private ExternalFundingAccount() { } // EF Core

    /// <summary>
    /// Creates a new external funding account attached to a wallet.
    /// </summary>
    public static ExternalFundingAccount Create(
        Guid walletId,
        PaymentProvider provider,
        string accountNumber,
        string accountName,
        string bankCode,
        string bankName,
        Currency currency,
        string? providerCustomerReference = null,
        string? providerAccountReference = null,
        bool isPrimary = false)
    {
        if (walletId == Guid.Empty)
            throw new ArgumentException("WalletId is required.", nameof(walletId));

        if (!Enum.IsDefined(provider))
            throw new ArgumentException($"Invalid payment provider '{provider}'.", nameof(provider));

        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new ArgumentException("AccountNumber cannot be empty.", nameof(accountNumber));

        if (string.IsNullOrWhiteSpace(accountName))
            throw new ArgumentException("AccountName cannot be empty.", nameof(accountName));

        if (string.IsNullOrWhiteSpace(bankCode))
            throw new ArgumentException("BankCode cannot be empty.", nameof(bankCode));

        if (string.IsNullOrWhiteSpace(bankName))
            throw new ArgumentException("BankName cannot be empty.", nameof(bankName));

        currency.EnsureTransactionalV1();

        return new ExternalFundingAccount
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            Provider = provider,
            AccountNumber = accountNumber.Trim(),
            AccountName = accountName.Trim(),
            BankCode = bankCode.Trim(),
            BankName = bankName.Trim(),
            Currency = currency,
            Status = ExternalFundingAccountStatus.Active,
            IsPrimary = isPrimary,
            ProviderCustomerReference = providerCustomerReference?.Trim(),
            ProviderAccountReference = providerAccountReference?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Sets or unsets this account as primary. Inactive or closed accounts cannot be primary.
    /// </summary>
    public void SetPrimary(bool isPrimary)
    {
        if (isPrimary && Status != ExternalFundingAccountStatus.Active)
        {
            throw new InvalidOperationException($"Cannot set external funding account as primary when status is {Status}. Only Active accounts can be primary.");
        }

        IsPrimary = isPrimary;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Clears the primary status of this account.
    /// </summary>
    public void ClearPrimary()
    {
        IsPrimary = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Activates the external funding account.
    /// </summary>
    public void MarkActive()
    {
        Status = ExternalFundingAccountStatus.Active;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Suspends the external funding account and revokes primary status if active.
    /// </summary>
    public void MarkSuspended()
    {
        Status = ExternalFundingAccountStatus.Suspended;
        if (IsPrimary)
        {
            IsPrimary = false;
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Closes the external funding account permanently and revokes primary status if active.
    /// </summary>
    public void MarkClosed()
    {
        Status = ExternalFundingAccountStatus.Closed;
        if (IsPrimary)
        {
            IsPrimary = false;
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
