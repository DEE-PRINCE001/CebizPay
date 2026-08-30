using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Wallet.ExternalAccounts;

/// <summary>
/// Handles <see cref="GetExternalFundingAccountsQuery"/>.
/// Retrieves external funding accounts attached to the authorized wallet.
/// Enforces tenant ownership boundaries.
/// </summary>
public sealed class GetExternalFundingAccountsQueryHandler
    : IRequestHandler<GetExternalFundingAccountsQuery, IReadOnlyList<ExternalFundingAccountResponseDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IExternalFundingAccountService _fundingAccountService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetExternalFundingAccountsQueryHandler"/>.
    /// </summary>
    public GetExternalFundingAccountsQueryHandler(
        IApplicationDbContext dbContext,
        IExternalFundingAccountService fundingAccountService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _fundingAccountService = fundingAccountService ?? throw new ArgumentNullException(nameof(fundingAccountService));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ExternalFundingAccountResponseDto>> Handle(
        GetExternalFundingAccountsQuery request,
        CancellationToken cancellationToken)
    {
        Domain.Finance.Entities.Wallet? wallet;

        if (request.OrganizationId.HasValue)
        {
            // Validate user is active member of the organization
            var isMember = await _dbContext.OrganizationMemberships
                .AnyAsync(m => m.OrganizationId == request.OrganizationId.Value
                            && m.UserId == request.CurrentUserId
                            && m.Status == MembershipStatus.Active,
                          cancellationToken);

            if (!isMember)
            {
                throw new TransferNotAuthorizedException("User is not an active member of the specified organization.");
            }

            var currency = request.Currency ?? Currency.NGN;
            wallet = await _dbContext.Wallets
                .FirstOrDefaultAsync(w => w.OrganizationId == request.OrganizationId.Value && w.Currency == currency, cancellationToken);
        }
        else
        {
            var currency = request.Currency ?? Currency.NGN;
            wallet = await _dbContext.Wallets
                .FirstOrDefaultAsync(w => w.IndividualId == request.CurrentUserId && w.Currency == currency, cancellationToken);
        }

        if (wallet == null)
        {
            return Array.Empty<ExternalFundingAccountResponseDto>();
        }

        var accounts = await _fundingAccountService.GetAccountsForWalletAsync(wallet.Id, cancellationToken);

        return accounts.Select(a => new ExternalFundingAccountResponseDto(
            Id: a.Id,
            WalletId: a.WalletId,
            Provider: a.Provider.ToString(),
            ProviderCustomerReference: a.ProviderCustomerReference,
            ProviderAccountReference: a.ProviderAccountReference,
            AccountNumber: a.AccountNumber,
            AccountName: a.AccountName,
            BankCode: a.BankCode,
            BankName: a.BankName,
            Currency: a.Currency.ToString(),
            Status: a.Status.ToString(),
            IsPrimary: a.IsPrimary,
            CreatedAtUtc: a.CreatedAtUtc,
            UpdatedAtUtc: a.UpdatedAtUtc)).ToList();
    }
}

/// <summary>
/// Handles <see cref="SetPrimaryExternalFundingAccountCommand"/>.
/// Sets an external funding account as primary for its wallet.
/// </summary>
public sealed class SetPrimaryExternalFundingAccountCommandHandler
    : IRequestHandler<SetPrimaryExternalFundingAccountCommand, ExternalFundingAccountResponseDto>
{
    private readonly IExternalFundingAccountService _fundingAccountService;

    /// <summary>
    /// Initializes a new instance of <see cref="SetPrimaryExternalFundingAccountCommandHandler"/>.
    /// </summary>
    public SetPrimaryExternalFundingAccountCommandHandler(IExternalFundingAccountService fundingAccountService)
    {
        _fundingAccountService = fundingAccountService ?? throw new ArgumentNullException(nameof(fundingAccountService));
    }

    /// <inheritdoc/>
    public async Task<ExternalFundingAccountResponseDto> Handle(
        SetPrimaryExternalFundingAccountCommand request,
        CancellationToken cancellationToken)
    {
        var result = await _fundingAccountService.SetPrimaryAccountAsync(
            request.AccountId,
            request.CurrentUserId,
            request.OrganizationId,
            cancellationToken);

        return new ExternalFundingAccountResponseDto(
            Id: result.Id,
            WalletId: result.WalletId,
            Provider: result.Provider.ToString(),
            ProviderCustomerReference: result.ProviderCustomerReference,
            ProviderAccountReference: result.ProviderAccountReference,
            AccountNumber: result.AccountNumber,
            AccountName: result.AccountName,
            BankCode: result.BankCode,
            BankName: result.BankName,
            Currency: result.Currency.ToString(),
            Status: result.Status.ToString(),
            IsPrimary: result.IsPrimary,
            CreatedAtUtc: result.CreatedAtUtc,
            UpdatedAtUtc: result.UpdatedAtUtc);
    }
}

/// <summary>
/// Handles <see cref="GetExternalFundingAccountByIdQuery"/>.
/// </summary>
public sealed class GetExternalFundingAccountByIdQueryHandler
    : IRequestHandler<GetExternalFundingAccountByIdQuery, ExternalFundingAccountResponseDto?>
{
    private readonly IExternalFundingAccountService _fundingAccountService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetExternalFundingAccountByIdQueryHandler"/>.
    /// </summary>
    public GetExternalFundingAccountByIdQueryHandler(IExternalFundingAccountService fundingAccountService)
    {
        _fundingAccountService = fundingAccountService ?? throw new ArgumentNullException(nameof(fundingAccountService));
    }

    /// <inheritdoc/>
    public async Task<ExternalFundingAccountResponseDto?> Handle(
        GetExternalFundingAccountByIdQuery request,
        CancellationToken cancellationToken)
    {
        var result = await _fundingAccountService.GetAccountByIdAsync(
            request.AccountId,
            request.CurrentUserId,
            request.OrganizationId,
            cancellationToken);

        if (result == null) return null;

        return new ExternalFundingAccountResponseDto(
            Id: result.Id,
            WalletId: result.WalletId,
            Provider: result.Provider.ToString(),
            ProviderCustomerReference: result.ProviderCustomerReference,
            ProviderAccountReference: result.ProviderAccountReference,
            AccountNumber: result.AccountNumber,
            AccountName: result.AccountName,
            BankCode: result.BankCode,
            BankName: result.BankName,
            Currency: result.Currency.ToString(),
            Status: result.Status.ToString(),
            IsPrimary: result.IsPrimary,
            CreatedAtUtc: result.CreatedAtUtc,
            UpdatedAtUtc: result.UpdatedAtUtc);
    }
}

/// <summary>
/// Handles <see cref="ProvisionMonnifyExternalFundingAccountCommand"/>.
/// Provisions a Monnify reserved virtual account and maps it as ExternalFundingAccount.
/// </summary>
public sealed class ProvisionMonnifyExternalFundingAccountCommandHandler
    : IRequestHandler<ProvisionMonnifyExternalFundingAccountCommand, ExternalFundingAccountResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IExternalFundingAccountService _fundingAccountService;

    /// <summary>
    /// Initializes a new instance of <see cref="ProvisionMonnifyExternalFundingAccountCommandHandler"/>.
    /// </summary>
    public ProvisionMonnifyExternalFundingAccountCommandHandler(
        IApplicationDbContext dbContext,
        IExternalFundingAccountService fundingAccountService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _fundingAccountService = fundingAccountService ?? throw new ArgumentNullException(nameof(fundingAccountService));
    }

    /// <inheritdoc/>
    public async Task<ExternalFundingAccountResponseDto> Handle(
        ProvisionMonnifyExternalFundingAccountCommand request,
        CancellationToken cancellationToken)
    {
        Domain.Finance.Entities.Wallet? wallet;

        if (request.OrganizationId.HasValue)
        {
            var isMember = await _dbContext.OrganizationMemberships
                .AnyAsync(m => m.OrganizationId == request.OrganizationId.Value
                            && m.UserId == request.CurrentUserId
                            && m.Status == MembershipStatus.Active,
                          cancellationToken);

            if (!isMember)
            {
                throw new TransferNotAuthorizedException("User is not an active member of the specified organization.");
            }

            wallet = await _dbContext.Wallets
                .FirstOrDefaultAsync(w => w.OrganizationId == request.OrganizationId.Value && w.Currency == request.Currency, cancellationToken);
        }
        else
        {
            wallet = await _dbContext.Wallets
                .FirstOrDefaultAsync(w => w.IndividualId == request.CurrentUserId && w.Currency == request.Currency, cancellationToken);
        }

        if (wallet == null)
        {
            throw new InvalidOperationException($"Wallet for currency '{request.Currency}' does not exist. Please initialize wallet first.");
        }

        var result = await _fundingAccountService.ProvisionMonnifyFundingAccountAsync(
            wallet.Id,
            request.CurrentUserId,
            request.OrganizationId,
            cancellationToken);

        return new ExternalFundingAccountResponseDto(
            Id: result.Id,
            WalletId: result.WalletId,
            Provider: result.Provider.ToString(),
            ProviderCustomerReference: result.ProviderCustomerReference,
            ProviderAccountReference: result.ProviderAccountReference,
            AccountNumber: result.AccountNumber,
            AccountName: result.AccountName,
            BankCode: result.BankCode,
            BankName: result.BankName,
            Currency: result.Currency.ToString(),
            Status: result.Status.ToString(),
            IsPrimary: result.IsPrimary,
            CreatedAtUtc: result.CreatedAtUtc,
            UpdatedAtUtc: result.UpdatedAtUtc);
    }
}

/// <summary>
/// Handles <see cref="DeactivateExternalFundingAccountCommand"/>.
/// </summary>
public sealed class DeactivateExternalFundingAccountCommandHandler
    : IRequestHandler<DeactivateExternalFundingAccountCommand, ExternalFundingAccountResponseDto>
{
    private readonly IExternalFundingAccountService _fundingAccountService;

    /// <summary>
    /// Initializes a new instance of <see cref="DeactivateExternalFundingAccountCommandHandler"/>.
    /// </summary>
    public DeactivateExternalFundingAccountCommandHandler(IExternalFundingAccountService fundingAccountService)
    {
        _fundingAccountService = fundingAccountService ?? throw new ArgumentNullException(nameof(fundingAccountService));
    }

    /// <inheritdoc/>
    public async Task<ExternalFundingAccountResponseDto> Handle(
        DeactivateExternalFundingAccountCommand request,
        CancellationToken cancellationToken)
    {
        var result = await _fundingAccountService.UpdateStatusAsync(
            request.AccountId,
            ExternalFundingAccountStatus.Suspended,
            request.CurrentUserId,
            request.OrganizationId,
            cancellationToken);

        return new ExternalFundingAccountResponseDto(
            Id: result.Id,
            WalletId: result.WalletId,
            Provider: result.Provider.ToString(),
            ProviderCustomerReference: result.ProviderCustomerReference,
            ProviderAccountReference: result.ProviderAccountReference,
            AccountNumber: result.AccountNumber,
            AccountName: result.AccountName,
            BankCode: result.BankCode,
            BankName: result.BankName,
            Currency: result.Currency.ToString(),
            Status: result.Status.ToString(),
            IsPrimary: result.IsPrimary,
            CreatedAtUtc: result.CreatedAtUtc,
            UpdatedAtUtc: result.UpdatedAtUtc);
    }
}

/// <summary>
/// Handles <see cref="GetFundingTransactionByIdQuery"/>.
/// </summary>
public sealed class GetFundingTransactionByIdQueryHandler
    : IRequestHandler<GetFundingTransactionByIdQuery, FundingTransactionResponseDto?>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetFundingTransactionByIdQueryHandler"/>.
    /// </summary>
    public GetFundingTransactionByIdQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<FundingTransactionResponseDto?> Handle(
        GetFundingTransactionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var funding = await _dbContext.FundingTransactions
            .FirstOrDefaultAsync(f => f.Id == request.FundingId, cancellationToken);

        if (funding == null) return null;

        var wallet = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.Id == funding.WalletId, cancellationToken);

        if (wallet == null) return null;

        // Tenant ownership check
        if (request.OrganizationId.HasValue)
        {
            if (wallet.OrganizationId != request.OrganizationId.Value)
            {
                throw new TransferNotAuthorizedException("Funding transaction belongs to a different organization.");
            }
        }
        else
        {
            if (wallet.IndividualId != request.CurrentUserId)
            {
                throw new TransferNotAuthorizedException("Funding transaction belongs to a different user.");
            }
        }

        return new FundingTransactionResponseDto(
            Id: funding.Id,
            WalletId: funding.WalletId,
            ExternalFundingAccountId: funding.ExternalFundingAccountId,
            Provider: funding.Provider.ToString(),
            ProviderTransactionReference: funding.ProviderTransactionReference,
            FundingChannel: funding.FundingChannel.ToString(),
            Amount: funding.Amount,
            FeeAmount: funding.FeeAmount,
            NetCreditedAmount: funding.NetCreditedAmount,
            Currency: funding.Currency.ToString(),
            Status: funding.Status.ToString(),
            LedgerTransactionId: funding.LedgerTransactionId,
            CreatedAtUtc: funding.CreatedAtUtc,
            CompletedAtUtc: funding.CompletedAtUtc,
            FailureReason: funding.FailureReason);
    }
}
