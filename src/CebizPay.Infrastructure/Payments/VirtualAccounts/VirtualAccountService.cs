using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Payments.Events;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Payments.VirtualAccounts;

/// <summary>
/// Service implementation for managing dedicated virtual accounts for individuals and organizations.
/// </summary>
public sealed partial class VirtualAccountService : IVirtualAccountService
{
    private readonly IEnumerable<IVirtualAccountProvider> _providers;
    private readonly ApplicationDbContext _dbContext;
    private readonly IOutboxService _outbox;
    private readonly ILogger<VirtualAccountService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualAccountService"/> class.
    /// </summary>
    public VirtualAccountService(
        IEnumerable<IVirtualAccountProvider> providers,
        ApplicationDbContext dbContext,
        IOutboxService outbox,
        ILogger<VirtualAccountService> logger)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private IVirtualAccountProvider GetProvider(PaymentProvider provider)
    {
        var match = _providers.FirstOrDefault(p => p.Provider == provider);
        if (match == null)
        {
            throw new InvalidOperationException($"No virtual account provider registered for '{provider}'.");
        }
        return match;
    }

    /// <inheritdoc/>
    public async Task<VirtualAccountDto> ProvisionIndividualVirtualAccountAsync(
        string individualId,
        Currency currency,
        PaymentProvider provider = PaymentProvider.Flutterwave,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(individualId))
            throw new ArgumentException("IndividualId is required.", nameof(individualId));

        currency.EnsureTransactionalV1();

        // Check for existing primary active virtual account (Idempotency)
        var existing = await _dbContext.VirtualAccounts
            .FirstOrDefaultAsync(v => v.IndividualId == individualId && v.Provider == provider && v.Currency == currency, cancellationToken)
            .ConfigureAwait(false);

        if (existing != null)
        {
            return MapToDto(existing);
        }

        // Retrieve profile info if available
        var profile = await _dbContext.IndividualProfiles
            .FirstOrDefaultAsync(p => p.UserId == individualId, cancellationToken)
            .ConfigureAwait(false);

        var accountName = profile != null
            ? $"{profile.FirstName} {profile.LastName}".Trim()
            : $"User {individualId}";
        var email = $"{individualId}@cebizpay.internal";
        string? phone = null;
        string? bvn = null;

        var providerAdapter = GetProvider(provider);
        var request = new VirtualAccountCreationRequest(
            OwnerIdentifier: individualId,
            AccountName: accountName,
            Email: email,
            PhoneNumber: phone,
            Currency: currency,
            Bvn: bvn);

        var result = await providerAdapter.CreateVirtualAccountAsync(request, cancellationToken).ConfigureAwait(false);
        string accountNumber;
        string bankCode;
        string bankName;
        string? providerReference;

        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.AccountNumber))
        {
            var providerName = provider.ToString();
            var errorMsg = result.ErrorMessage ?? "Unknown error";
            LogIndividualProvisioningFailed(_logger, individualId, providerName, errorMsg);
            throw new InvalidOperationException($"Virtual account provisioning failed via {provider}: {errorMsg}");
        }

        accountNumber = result.AccountNumber;
        bankCode = result.BankCode ?? "035";
        bankName = result.BankName ?? "Partner Bank";
        providerReference = result.ProviderReference;

        var virtualAccount = VirtualAccount.CreateIndividual(
            individualId: individualId,
            provider: provider,
            accountNumber: accountNumber,
            accountName: result?.AccountName ?? accountName,
            bankCode: bankCode,
            bankName: bankName,
            currency: currency,
            providerReference: providerReference);

        _dbContext.VirtualAccounts.Add(virtualAccount);

        var audit = AuditLog.Create(
            actorId: individualId,
            action: AuditActions.VirtualAccountCreated,
            resourceType: AuditResourceTypes.VirtualAccount,
            resourceId: virtualAccount.Id.ToString(),
            afterJson: $"{{\"provider\":\"{provider}\",\"accountNumber\":\"{virtualAccount.AccountNumber}\"}}");
        _dbContext.AuditLogs.Add(audit);

        _outbox.Write(new VirtualAccountProvisionedDomainEvent(
            VirtualAccountId: virtualAccount.Id,
            IndividualId: individualId,
            OrganizationId: null,
            Provider: provider,
            AccountNumber: virtualAccount.AccountNumber,
            BankCode: virtualAccount.BankCode,
            Currency: currency,
            OccurredOnUtc: DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogIndividualProvisioningSuccess(_logger, virtualAccount.AccountNumber, individualId);
        return MapToDto(virtualAccount);
    }

    /// <inheritdoc/>
    public async Task<VirtualAccountDto> ProvisionOrganizationVirtualAccountAsync(
        Guid organizationId,
        Currency currency,
        PaymentProvider provider = PaymentProvider.Flutterwave,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));

        currency.EnsureTransactionalV1();

        // Check for existing primary active virtual account (Idempotency)
        var existing = await _dbContext.VirtualAccounts
            .FirstOrDefaultAsync(v => v.OrganizationId == organizationId && v.Provider == provider && v.Currency == currency, cancellationToken)
            .ConfigureAwait(false);

        if (existing != null)
        {
            return MapToDto(existing);
        }

        var org = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken)
            .ConfigureAwait(false);

        var accountName = org?.CompanyName ?? $"Org {organizationId}";
        var email = org?.Email ?? $"org_{organizationId:N}@cebizpay.internal";
        var phone = org?.Phone;

        var providerAdapter = GetProvider(provider);
        var request = new VirtualAccountCreationRequest(
            OwnerIdentifier: organizationId.ToString(),
            AccountName: accountName,
            Email: email,
            PhoneNumber: phone,
            Currency: currency);

        var result = await providerAdapter.CreateVirtualAccountAsync(request, cancellationToken).ConfigureAwait(false);
        string accountNumber;
        string bankCode;
        string bankName;
        string? providerReference;

        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.AccountNumber))
        {
            var providerName = provider.ToString();
            var errorMsg = result.ErrorMessage ?? "Unknown error";
            LogOrganizationProvisioningFailed(_logger, organizationId, providerName, errorMsg);
            throw new InvalidOperationException($"Virtual account provisioning failed via {provider}: {errorMsg}");
        }

        accountNumber = result.AccountNumber;
        bankCode = result.BankCode ?? "035";
        bankName = result.BankName ?? "Partner Bank";
        providerReference = result.ProviderReference;

        var virtualAccount = VirtualAccount.CreateOrganization(
            organizationId: organizationId,
            provider: provider,
            accountNumber: accountNumber,
            accountName: result?.AccountName ?? accountName,
            bankCode: bankCode,
            bankName: bankName,
            currency: currency,
            providerReference: providerReference);

        _dbContext.VirtualAccounts.Add(virtualAccount);

        var audit = AuditLog.Create(
            actorId: organizationId.ToString(),
            action: AuditActions.VirtualAccountCreated,
            resourceType: AuditResourceTypes.VirtualAccount,
            resourceId: virtualAccount.Id.ToString(),
            afterJson: $"{{\"provider\":\"{provider}\",\"accountNumber\":\"{virtualAccount.AccountNumber}\"}}",
            organizationId: organizationId);
        _dbContext.AuditLogs.Add(audit);

        _outbox.Write(new VirtualAccountProvisionedDomainEvent(
            VirtualAccountId: virtualAccount.Id,
            IndividualId: null,
            OrganizationId: organizationId,
            Provider: provider,
            AccountNumber: virtualAccount.AccountNumber,
            BankCode: virtualAccount.BankCode,
            Currency: currency,
            OccurredOnUtc: DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogOrganizationProvisioningSuccess(_logger, virtualAccount.AccountNumber, organizationId);
        return MapToDto(virtualAccount);
    }

    /// <inheritdoc/>
    public async Task<VirtualAccountDto?> GetVirtualAccountForOwnerAsync(
        string? individualId,
        Guid? organizationId,
        Currency currency,
        CancellationToken cancellationToken = default)
    {
        currency.EnsureTransactionalV1();

        var query = _dbContext.VirtualAccounts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(individualId))
        {
            query = query.Where(v => v.IndividualId == individualId && v.Currency == currency);
        }
        else if (organizationId.HasValue && organizationId != Guid.Empty)
        {
            query = query.Where(v => v.OrganizationId == organizationId.Value && v.Currency == currency);
        }
        else
        {
            return null;
        }

        var match = await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return match != null ? MapToDto(match) : null;
    }

    /// <inheritdoc/>
    public async Task<VirtualAccountDto?> GetVirtualAccountByNumberAsync(
        PaymentProvider provider,
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return null;

        var cleanNumber = accountNumber.Trim();
        var match = await _dbContext.VirtualAccounts
            .FirstOrDefaultAsync(v => v.Provider == provider && v.AccountNumber == cleanNumber, cancellationToken)
            .ConfigureAwait(false);

        return match != null ? MapToDto(match) : null;
    }

    private static VirtualAccountDto MapToDto(VirtualAccount entity) =>
        new(
            Id: entity.Id,
            IndividualId: entity.IndividualId,
            OrganizationId: entity.OrganizationId,
            Provider: entity.Provider,
            AccountNumber: entity.AccountNumber,
            AccountName: entity.AccountName,
            BankCode: entity.BankCode,
            BankName: entity.BankName,
            Currency: entity.Currency,
            Status: entity.Status,
            CreatedAtUtc: entity.CreatedAtUtc);

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to provision individual virtual account for {IndividualId} with {Provider}: {Error}")]
    private static partial void LogIndividualProvisioningFailed(ILogger logger, string individualId, string provider, string error);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Successfully provisioned virtual account {AccountNumber} for individual {IndividualId}")]
    private static partial void LogIndividualProvisioningSuccess(ILogger logger, string accountNumber, string individualId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed to provision organization virtual account for {OrgId} with {Provider}: {Error}")]
    private static partial void LogOrganizationProvisioningFailed(ILogger logger, Guid orgId, string provider, string error);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Successfully provisioned virtual account {AccountNumber} for organization {OrgId}")]
    private static partial void LogOrganizationProvisioningSuccess(ILogger logger, string accountNumber, Guid orgId);
}
