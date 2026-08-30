using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Finance.Entities;

/// <summary>
/// Domain aggregate representation of a financial wallet.
/// Owned by an Individual or Organization. Holds a single currency balance.
/// Invariant: AvailableBalance >= 0.
/// </summary>
public class Wallet
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Owner Individual ID if user wallet.</summary>
    public string? IndividualId { get; private set; }

    /// <summary>Owner Organization ID if B2B org wallet.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Wallet currency.</summary>
    public Currency Currency { get; private set; }

    /// <summary>Materialized current available balance.</summary>
    public decimal AvailableBalance { get; private set; }

    /// <summary>Wallet status (Active, Frozen, Closed).</summary>
    public WalletStatus Status { get; private set; } = WalletStatus.Active;

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Updated timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>External funding rails/accounts attached to this wallet.</summary>
    public ICollection<ExternalFundingAccount> ExternalFundingAccounts { get; private set; } = new List<ExternalFundingAccount>();

    private Wallet() { } // EF Core

    /// <summary>
    /// Creates an individual wallet.
    /// </summary>
    public static Wallet CreateIndividualWallet(string individualId, Currency currency)
    {
        if (string.IsNullOrWhiteSpace(individualId))
            throw new ArgumentException("IndividualId is required.", nameof(individualId));

        currency.EnsureTransactionalV1();

        return new Wallet
        {
            Id = Guid.NewGuid(),
            IndividualId = individualId,
            OrganizationId = null,
            Currency = currency,
            AvailableBalance = 0m,
            Status = WalletStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates an organization wallet.
    /// </summary>
    public static Wallet CreateOrganizationWallet(Guid organizationId, Currency currency)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));

        currency.EnsureTransactionalV1();

        return new Wallet
        {
            Id = Guid.NewGuid(),
            IndividualId = null,
            OrganizationId = organizationId,
            Currency = currency,
            AvailableBalance = 0m,
            Status = WalletStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Credits the wallet balance.
    /// </summary>
    public void Credit(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Credit amount must be positive.", nameof(amount));
        if (Status != WalletStatus.Active)
            throw new InvalidOperationException($"Cannot credit wallet with status {Status}.");

        AvailableBalance += amount;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Debits the wallet balance with non-negative balance invariant check.
    /// </summary>
    public void Debit(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Debit amount must be positive.", nameof(amount));
        if (Status != WalletStatus.Active)
            throw new InvalidOperationException($"Cannot debit wallet with status {Status}.");
        if (AvailableBalance < amount)
            throw new InvalidOperationException($"Insufficient available balance. Requested: {amount}, Available: {AvailableBalance}.");

        AvailableBalance -= amount;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Freezes the wallet.
    /// </summary>
    public void Freeze()
    {
        Status = WalletStatus.Frozen;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Unfreezes the wallet.
    /// </summary>
    public void Unfreeze()
    {
        Status = WalletStatus.Active;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
