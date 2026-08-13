using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Finance;

/// <summary>
/// Infrastructure service for managing customer wallets and their associated 1-to-1 ledger accounts.
/// </summary>
public sealed class WalletService : IWalletService
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="WalletService"/>.
    /// </summary>
    public WalletService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<Wallet> GetOrCreateIndividualWalletAsync(string individualId, Currency currency, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(individualId))
            throw new ArgumentException("IndividualId is required.", nameof(individualId));

        currency.EnsureTransactionalV1();

        var existingWallet = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.IndividualId == individualId && w.Currency == currency, cancellationToken);

        if (existingWallet != null)
        {
            return existingWallet;
        }

        var wallet = Wallet.CreateIndividualWallet(individualId, currency);
        var ledgerAccount = LedgerAccount.CreateWalletAccount(wallet.Id, $"Individual Wallet ({currency})", currency);

        _dbContext.Wallets.Add(wallet);
        _dbContext.LedgerAccounts.Add(ledgerAccount);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return wallet;
    }

    /// <inheritdoc/>
    public async Task<Wallet> GetOrCreateOrganizationWalletAsync(Guid organizationId, Currency currency, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));

        currency.EnsureTransactionalV1();

        var existingWallet = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.OrganizationId == organizationId && w.Currency == currency, cancellationToken);

        if (existingWallet != null)
        {
            return existingWallet;
        }

        var wallet = Wallet.CreateOrganizationWallet(organizationId, currency);
        var ledgerAccount = LedgerAccount.CreateWalletAccount(wallet.Id, $"Organization Wallet ({currency})", currency);

        _dbContext.Wallets.Add(wallet);
        _dbContext.LedgerAccounts.Add(ledgerAccount);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return wallet;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Wallet>> GetIndividualWalletsAsync(string individualId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Wallets
            .Where(w => w.IndividualId == individualId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Wallet>> GetOrganizationWalletsAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Wallets
            .Where(w => w.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
    }
}
