using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.Common.Interfaces.Finance;

/// <summary>
/// Service contract for wallet management and creation.
/// Enforces: 1 active wallet per owner per currency.
/// </summary>
public interface IWalletService
{
    /// <summary>
    /// Creates or retrieves an individual wallet for a specific currency.
    /// </summary>
    Task<Wallet> GetOrCreateIndividualWalletAsync(string individualId, Currency currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or retrieves an organization wallet for a specific currency.
    /// </summary>
    Task<Wallet> GetOrCreateOrganizationWalletAsync(Guid organizationId, Currency currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves active wallets owned by an individual.
    /// </summary>
    Task<IReadOnlyList<Wallet>> GetIndividualWalletsAsync(string individualId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves active wallets owned by an organization.
    /// </summary>
    Task<IReadOnlyList<Wallet>> GetOrganizationWalletsAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
