using System.Text.Json;
using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Finance.Events;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Finance;

/// <summary>
/// Infrastructure service implementation for managing wallet external funding accounts,
/// enforcing tenant isolation, database concurrency guarantees, and audit trail integrity.
/// </summary>
public sealed partial class ExternalFundingAccountService : IExternalFundingAccountService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IOutboxService _outboxService;
    private readonly IEnumerable<IVirtualAccountProvider> _virtualAccountProviders;
    private readonly ILogger<ExternalFundingAccountService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalFundingAccountService"/> class.
    /// </summary>
    public ExternalFundingAccountService(
        ApplicationDbContext dbContext,
        IOutboxService outboxService,
        IEnumerable<IVirtualAccountProvider> virtualAccountProviders,
        ILogger<ExternalFundingAccountService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _virtualAccountProviders = virtualAccountProviders ?? Enumerable.Empty<IVirtualAccountProvider>();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<ExternalFundingAccountDto> CreateAccountAsync(
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
        CancellationToken cancellationToken = default)
    {
        var wallet = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.Id == walletId, cancellationToken)
            .ConfigureAwait(false);

        if (wallet == null)
        {
            throw new InvalidOperationException($"Wallet '{walletId}' does not exist.");
        }

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        if (_dbContext.Database.IsRelational())
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            if (isPrimary)
            {
                // Unset any existing primary accounts for this wallet
                var existingPrimaries = await _dbContext.ExternalFundingAccounts
                    .Where(a => a.WalletId == walletId && a.IsPrimary)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (existingPrimaries.Count > 0)
                {
                    foreach (var primary in existingPrimaries)
                    {
                        primary.ClearPrimary();
                    }
                    await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            var account = ExternalFundingAccount.Create(
                walletId: walletId,
                provider: provider,
                accountNumber: accountNumber,
                accountName: accountName,
                bankCode: bankCode,
                bankName: bankName,
                currency: currency,
                providerCustomerReference: providerCustomerReference,
                providerAccountReference: providerAccountReference,
                isPrimary: isPrimary);

            _dbContext.ExternalFundingAccounts.Add(account);

            // Audit
            var actorId = wallet.IndividualId ?? wallet.OrganizationId?.ToString() ?? "SYSTEM";
            var audit = AuditLog.Create(
                actorId: actorId,
                action: AuditActions.ExternalFundingAccountCreated,
                resourceType: AuditResourceTypes.ExternalFundingAccount,
                resourceId: account.Id.ToString(),
                afterJson: JsonSerializer.Serialize(new
                {
                    WalletId = walletId,
                    Provider = provider.ToString(),
                    AccountNumber = MaskAccountNumber(accountNumber),
                    AccountName = accountName,
                    BankCode = bankCode,
                    BankName = bankName,
                    Currency = currency.ToString(),
                    IsPrimary = isPrimary
                }),
                organizationId: wallet.OrganizationId);

            _dbContext.AuditLogs.Add(audit);

            _outboxService.Write(new ExternalFundingAccountCreatedDomainEvent(
                AccountId: account.Id,
                WalletId: account.WalletId,
                Provider: account.Provider,
                AccountNumber: account.AccountNumber,
                BankCode: account.BankCode,
                IsPrimary: account.IsPrimary,
                OccurredOnUtc: DateTime.UtcNow));

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            LogAccountCreated(_logger, account.Id, walletId, provider);
            return MapToDto(account);
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
            LogAccountCreationFailure(_logger, walletId, ex);
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ExternalFundingAccountDto>> GetAccountsForWalletAsync(
        Guid walletId,
        CancellationToken cancellationToken = default)
    {
        var accounts = await _dbContext.ExternalFundingAccounts
            .AsNoTracking()
            .Where(a => a.WalletId == walletId)
            .OrderByDescending(a => a.IsPrimary)
            .ThenByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return accounts.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<ExternalFundingAccountDto?> GetPrimaryAccountForWalletAsync(
        Guid walletId,
        CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.ExternalFundingAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.WalletId == walletId && a.IsPrimary && a.Status == ExternalFundingAccountStatus.Active, cancellationToken)
            .ConfigureAwait(false);

        return account == null ? null : MapToDto(account);
    }

    /// <inheritdoc/>
    public async Task<ExternalFundingAccountDto?> GetAccountByIdAsync(
        Guid accountId,
        string actorUserId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.ExternalFundingAccounts
            .Include(a => a.Wallet)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            .ConfigureAwait(false);

        if (account == null) return null;

        var wallet = account.Wallet ?? await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == account.WalletId, cancellationToken).ConfigureAwait(false);
        if (wallet == null) return null;

        await ValidateWalletOwnershipAsync(wallet, actorUserId, organizationId, cancellationToken).ConfigureAwait(false);

        return MapToDto(account);
    }

    /// <inheritdoc/>
    public async Task<ExternalFundingAccountDto> ProvisionMonnifyFundingAccountAsync(
        Guid walletId,
        string actorUserId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        var wallet = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.Id == walletId, cancellationToken)
            .ConfigureAwait(false);

        if (wallet == null)
            throw new InvalidOperationException($"Wallet '{walletId}' does not exist.");

        await ValidateWalletOwnershipAsync(wallet, actorUserId, organizationId, cancellationToken).ConfigureAwait(false);

        // Check if an active Monnify account already exists for this wallet (Idempotency)
        var existing = await _dbContext.ExternalFundingAccounts
            .FirstOrDefaultAsync(a => a.WalletId == walletId && a.Provider == PaymentProvider.Monnify && a.Status == ExternalFundingAccountStatus.Active, cancellationToken)
            .ConfigureAwait(false);

        if (existing != null)
        {
            return MapToDto(existing);
        }

        // Gather minimum required customer information from internal profile
        string accountName;
        string email;
        string? phone = null;
        string? bvn = null;
        string ownerId;

        if (wallet.OrganizationId.HasValue)
        {
            var org = await _dbContext.Organizations
                .FirstOrDefaultAsync(o => o.Id == wallet.OrganizationId.Value, cancellationToken)
                .ConfigureAwait(false);

            accountName = org != null ? org.CompanyName : $"Org {wallet.OrganizationId.Value:N}";
            email = org != null && !string.IsNullOrWhiteSpace(org.Email) ? org.Email : $"org_{wallet.OrganizationId.Value:N}@cebizpay.internal";
            phone = org?.Phone;
            ownerId = wallet.OrganizationId.Value.ToString();
        }
        else
        {
            var profile = await _dbContext.IndividualProfiles
                .FirstOrDefaultAsync(p => p.UserId == wallet.IndividualId, cancellationToken)
                .ConfigureAwait(false);

            accountName = profile != null ? $"{profile.FirstName} {profile.LastName}".Trim() : $"User {wallet.IndividualId}";
            email = $"{wallet.IndividualId}@cebizpay.internal";
            ownerId = wallet.IndividualId ?? actorUserId;
        }

        var providerAdapter = _virtualAccountProviders.FirstOrDefault(p => p.Provider == PaymentProvider.Monnify);
        if (providerAdapter == null)
        {
            throw new InvalidOperationException("Monnify virtual account provider adapter is not registered.");
        }

        var creationRequest = new Application.Common.Interfaces.Payments.VirtualAccountCreationRequest(
            OwnerIdentifier: ownerId,
            AccountName: accountName,
            Email: email,
            PhoneNumber: phone,
            Currency: wallet.Currency,
            Bvn: bvn);

        var result = await providerAdapter.CreateVirtualAccountAsync(creationRequest, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.AccountNumber))
        {
            var errorMsg = result.ErrorMessage ?? "Unknown provider error";
            LogMonnifyProvisioningFailure(_logger, walletId, errorMsg);
            throw new InvalidOperationException($"Monnify reserved account provisioning failed: {errorMsg}");
        }

        // Determine if this should be the primary account
        var hasExistingPrimary = await _dbContext.ExternalFundingAccounts
            .AnyAsync(a => a.WalletId == walletId && a.IsPrimary, cancellationToken)
            .ConfigureAwait(false);

        var isPrimary = !hasExistingPrimary;

        return await CreateAccountAsync(
            walletId: walletId,
            provider: PaymentProvider.Monnify,
            accountNumber: result.AccountNumber,
            accountName: result.AccountName ?? accountName,
            bankCode: result.BankCode ?? "035",
            bankName: result.BankName ?? "Wema Bank",
            currency: wallet.Currency,
            providerCustomerReference: ownerId,
            providerAccountReference: result.ProviderReference,
            isPrimary: isPrimary,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ExternalFundingAccountDto> SetPrimaryAccountAsync(
        Guid accountId,
        string actorUserId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        if (_dbContext.Database.IsRelational())
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var account = await _dbContext.ExternalFundingAccounts
                .Include(a => a.Wallet)
                .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
                .ConfigureAwait(false);

            if (account == null)
            {
                throw new InvalidOperationException($"External funding account '{accountId}' not found.");
            }

            var wallet = account.Wallet;
            if (wallet == null)
            {
                wallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == account.WalletId, cancellationToken).ConfigureAwait(false);
                if (wallet == null)
                {
                    throw new InvalidOperationException($"Associated wallet '{account.WalletId}' not found.");
                }
            }

            // Tenant authorization validation
            await ValidateWalletOwnershipAsync(wallet, actorUserId, organizationId, cancellationToken).ConfigureAwait(false);

            if (account.Status != ExternalFundingAccountStatus.Active)
            {
                throw new InvalidOperationException(
                    $"Cannot set external funding account as primary when status is '{account.Status}'. Only Active accounts can be primary.");
            }

            // Step 1: Unset any other primary accounts first and flush to avoid transient unique constraint collision
            var existingPrimaries = await _dbContext.ExternalFundingAccounts
                .Where(a => a.WalletId == account.WalletId && a.IsPrimary && a.Id != accountId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (existingPrimaries.Count > 0)
            {
                foreach (var existing in existingPrimaries)
                {
                    existing.ClearPrimary();
                }
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            // Step 2: Set target account as primary
            account.SetPrimary(true);

            var audit = AuditLog.Create(
                actorId: actorUserId,
                action: AuditActions.ExternalFundingAccountPrimaryChanged,
                resourceType: AuditResourceTypes.ExternalFundingAccount,
                resourceId: account.Id.ToString(),
                afterJson: JsonSerializer.Serialize(new
                {
                    AccountId = account.Id,
                    WalletId = account.WalletId,
                    IsPrimary = true
                }),
                organizationId: wallet.OrganizationId);

            _dbContext.AuditLogs.Add(audit);

            _outboxService.Write(new ExternalFundingAccountPrimaryChangedDomainEvent(
                AccountId: account.Id,
                WalletId: account.WalletId,
                Provider: account.Provider,
                IsPrimary: true,
                OccurredOnUtc: DateTime.UtcNow));

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            LogPrimaryChanged(_logger, account.Id, account.WalletId);
            return MapToDto(account);
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
            LogPrimaryChangeFailure(_logger, accountId, ex);
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<ExternalFundingAccountDto> UpdateStatusAsync(
        Guid accountId,
        ExternalFundingAccountStatus newStatus,
        string actorUserId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

        var account = await _dbContext.ExternalFundingAccounts
            .Include(a => a.Wallet)
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            .ConfigureAwait(false);

        if (account == null)
        {
            throw new InvalidOperationException($"External funding account '{accountId}' not found.");
        }

        var wallet = account.Wallet ?? await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == account.WalletId, cancellationToken).ConfigureAwait(false);
        if (wallet == null)
        {
            throw new InvalidOperationException($"Associated wallet '{account.WalletId}' not found.");
        }

        await ValidateWalletOwnershipAsync(wallet, actorUserId, organizationId, cancellationToken).ConfigureAwait(false);

        switch (newStatus)
        {
            case ExternalFundingAccountStatus.Active:
                account.MarkActive();
                break;
            case ExternalFundingAccountStatus.Suspended:
                account.MarkSuspended();
                break;
            case ExternalFundingAccountStatus.Closed:
                account.MarkClosed();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(newStatus), $"Unsupported status: {newStatus}");
        }

        var auditAction = newStatus switch
        {
            ExternalFundingAccountStatus.Active => AuditActions.ExternalFundingAccountActivated,
            _ => AuditActions.ExternalFundingAccountDeactivated
        };

        var audit = AuditLog.Create(
            actorId: actorUserId,
            action: auditAction,
            resourceType: AuditResourceTypes.ExternalFundingAccount,
            resourceId: account.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new
            {
                AccountId = account.Id,
                NewStatus = newStatus.ToString()
            }),
            organizationId: wallet.OrganizationId);

        _dbContext.AuditLogs.Add(audit);

        _outboxService.Write(new ExternalFundingAccountStatusChangedDomainEvent(
            AccountId: account.Id,
            WalletId: account.WalletId,
            Status: account.Status,
            OccurredOnUtc: DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToDto(account);
    }

    private async Task ValidateWalletOwnershipAsync(
        Wallet wallet,
        string actorUserId,
        Guid? organizationId,
        CancellationToken cancellationToken)
    {
        if (organizationId.HasValue || wallet.OrganizationId.HasValue)
        {
            var targetOrgId = organizationId ?? wallet.OrganizationId!.Value;

            if (wallet.OrganizationId != targetOrgId)
            {
                throw new TransferNotAuthorizedException("The external funding account does not belong to the specified organization.");
            }

            var isMember = await _dbContext.OrganizationMemberships
                .AnyAsync(m => m.OrganizationId == targetOrgId
                            && m.UserId == actorUserId
                            && m.Status == MembershipStatus.Active,
                          cancellationToken)
                .ConfigureAwait(false);

            if (!isMember)
            {
                throw new TransferNotAuthorizedException("User is not an active member authorized for this organization wallet.");
            }
        }
        else
        {
            if (wallet.IndividualId != actorUserId)
            {
                throw new TransferNotAuthorizedException("The external funding account does not belong to the authenticated user.");
            }
        }
    }

    private static string MaskAccountNumber(string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber) || accountNumber.Length <= 4)
            return "****";
        return new string('*', accountNumber.Length - 4) + accountNumber[^4..];
    }

    private static ExternalFundingAccountDto MapToDto(ExternalFundingAccount account) =>
        new(
            Id: account.Id,
            WalletId: account.WalletId,
            Provider: account.Provider,
            ProviderName: account.Provider.ToString(),
            ProviderCustomerReference: account.ProviderCustomerReference,
            ProviderAccountReference: account.ProviderAccountReference,
            AccountNumber: account.AccountNumber,
            AccountName: account.AccountName,
            BankCode: account.BankCode,
            BankName: account.BankName,
            Currency: account.Currency,
            CurrencyCode: account.Currency.ToString(),
            Status: account.Status,
            StatusName: account.Status.ToString(),
            IsPrimary: account.IsPrimary,
            CreatedAtUtc: account.CreatedAtUtc,
            UpdatedAtUtc: account.UpdatedAtUtc);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "External funding account {AccountId} created for wallet {WalletId} with provider {Provider}")]
    private static partial void LogAccountCreated(ILogger logger, Guid accountId, Guid walletId, PaymentProvider provider);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to create external funding account for wallet {WalletId}")]
    private static partial void LogAccountCreationFailure(ILogger logger, Guid walletId, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "External funding account {AccountId} designated primary for wallet {WalletId}")]
    private static partial void LogPrimaryChanged(ILogger logger, Guid accountId, Guid walletId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Failed to update primary external funding account {AccountId}")]
    private static partial void LogPrimaryChangeFailure(ILogger logger, Guid accountId, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Monnify reserved account provisioning failed for wallet {WalletId}: {Error}")]
    private static partial void LogMonnifyProvisioningFailure(ILogger logger, Guid walletId, string error);
}
