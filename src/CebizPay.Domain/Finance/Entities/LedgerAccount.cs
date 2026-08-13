using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Finance.Entities;

/// <summary>
/// Domain entity representing a double-entry ledger account.
/// Tied 1-to-1 with a Wallet or created as a platform System/Settlement account.
/// </summary>
public class LedgerAccount
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Tied wallet ID if customer wallet account.</summary>
    public Guid? WalletId { get; private set; }

    /// <summary>Account name description.</summary>
    public string AccountName { get; private set; } = string.Empty;

    /// <summary>Account currency.</summary>
    public Currency Currency { get; private set; }

    /// <summary>Ledger account type.</summary>
    public LedgerAccountType AccountType { get; private set; }

    /// <summary>Account status.</summary>
    public LedgerAccountStatus Status { get; private set; } = LedgerAccountStatus.Active;

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private LedgerAccount() { } // EF Core

    /// <summary>
    /// Creates a customer wallet ledger account.
    /// </summary>
    public static LedgerAccount CreateWalletAccount(Guid walletId, string accountName, Currency currency)
    {
        if (walletId == Guid.Empty)
            throw new ArgumentException("WalletId is required for customer wallet accounts.", nameof(walletId));
        if (string.IsNullOrWhiteSpace(accountName))
            throw new ArgumentException("AccountName is required.", nameof(accountName));

        currency.EnsureTransactionalV1();

        return new LedgerAccount
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            AccountName = accountName.Trim(),
            Currency = currency,
            AccountType = LedgerAccountType.CustomerWallet,
            Status = LedgerAccountStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a system or settlement ledger account.
    /// </summary>
    public static LedgerAccount CreateSystemAccount(string accountName, Currency currency, LedgerAccountType accountType)
    {
        if (string.IsNullOrWhiteSpace(accountName))
            throw new ArgumentException("AccountName is required.", nameof(accountName));
        if (accountType == LedgerAccountType.CustomerWallet)
            throw new ArgumentException("System accounts cannot have CustomerWallet type.", nameof(accountType));

        currency.EnsureTransactionalV1();

        return new LedgerAccount
        {
            Id = Guid.NewGuid(),
            WalletId = null,
            AccountName = accountName.Trim(),
            Currency = currency,
            AccountType = accountType,
            Status = LedgerAccountStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
