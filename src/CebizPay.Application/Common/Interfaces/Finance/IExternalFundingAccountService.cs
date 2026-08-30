using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Finance;

/// <summary>
/// Service abstraction for managing external funding accounts attached to wallets.
/// </summary>
public interface IExternalFundingAccountService
{
    /// <summary>
    /// Creates and attaches a new external funding account to the specified wallet.
    /// </summary>
    Task<ExternalFundingAccountDto> CreateAccountAsync(
        Guid walletId,
        PaymentProvider provider,
        string accountNumber,
        string accountName,
        string bankCode,
        string bankName,
        Currency currency,
        string? providerCustomerReference = null,
        string? providerAccountReference = null,
        bool isPrimary = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all external funding accounts attached to the specified wallet.
    /// </summary>
    Task<IReadOnlyList<ExternalFundingAccountDto>> GetAccountsForWalletAsync(
        Guid walletId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the primary external funding account attached to the specified wallet, if one exists.
    /// </summary>
    Task<ExternalFundingAccountDto?> GetPrimaryAccountForWalletAsync(
        Guid walletId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific external funding account by ID, verifying tenant authorization.
    /// </summary>
    Task<ExternalFundingAccountDto?> GetAccountByIdAsync(
        Guid accountId,
        string actorUserId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Provisions a new provider-backed Monnify reserved virtual account and attaches it as an ExternalFundingAccount to the wallet.
    /// </summary>
    Task<ExternalFundingAccountDto> ProvisionMonnifyFundingAccountAsync(
        Guid walletId,
        string actorUserId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Designates an existing external funding account as primary for its wallet, atomically unsetting any previous primary account.
    /// </summary>
    Task<ExternalFundingAccountDto> SetPrimaryAccountAsync(
        Guid accountId,
        string actorUserId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the lifecycle status of an external funding account (Active, Suspended, Closed).
    /// </summary>
    Task<ExternalFundingAccountDto> UpdateStatusAsync(
        Guid accountId,
        ExternalFundingAccountStatus newStatus,
        string actorUserId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);
}
