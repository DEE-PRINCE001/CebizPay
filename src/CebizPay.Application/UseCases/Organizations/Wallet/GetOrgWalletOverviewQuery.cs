using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Finance.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Wallet;

/// <summary>
/// Query to retrieve the corporate wallet overview and dedicated funding account details for an organization.
/// </summary>
public sealed record GetOrgWalletOverviewQuery(Guid OrganizationId) : IRequest<OrgWalletOverviewDto?>;

/// <summary>
/// Validator for <see cref="GetOrgWalletOverviewQuery"/>.
/// </summary>
public sealed class GetOrgWalletOverviewQueryValidator : AbstractValidator<GetOrgWalletOverviewQuery>
{
    /// <summary>
    /// Initializes validation rules.
    /// </summary>
    public GetOrgWalletOverviewQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
    }
}

/// <summary>
/// Handler for <see cref="GetOrgWalletOverviewQuery"/>.
/// </summary>
public sealed class GetOrgWalletOverviewQueryHandler : IRequestHandler<GetOrgWalletOverviewQuery, OrgWalletOverviewDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetOrgWalletOverviewQueryHandler"/>.
    /// </summary>
    public GetOrgWalletOverviewQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    /// <inheritdoc/>
    public async Task<OrgWalletOverviewDto?> Handle(GetOrgWalletOverviewQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var org = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId && !o.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (org == null)
        {
            return null;
        }

        var wallet = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.OrganizationId == request.OrganizationId && w.Currency == Currency.NGN, cancellationToken)
            .ConfigureAwait(false);

        if (wallet == null)
        {
            return null;
        }

        // Check ExternalFundingAccounts first (Monnify / Paystack dedicated accounts)
        var externalFundingAccount = await _dbContext.ExternalFundingAccounts
            .FirstOrDefaultAsync(e => e.WalletId == wallet.Id && e.Status == ExternalFundingAccountStatus.Active, cancellationToken)
            .ConfigureAwait(false);

        // Fallback to VirtualAccounts if not in ExternalFundingAccounts
        var virtualAccount = externalFundingAccount == null
            ? await _dbContext.VirtualAccounts
                .FirstOrDefaultAsync(v => v.OrganizationId == request.OrganizationId && v.Currency == Currency.NGN, cancellationToken)
                .ConfigureAwait(false)
            : null;

        var accountNumber = externalFundingAccount?.AccountNumber ?? virtualAccount?.AccountNumber;
        var accountName = externalFundingAccount?.AccountName ?? virtualAccount?.AccountName ?? org.CompanyName;
        var bankName = externalFundingAccount?.BankName ?? virtualAccount?.BankName ?? "Wema Bank / CebizPay";
        var bankCode = externalFundingAccount?.BankCode ?? virtualAccount?.BankCode ?? "035";

        return new OrgWalletOverviewDto(
            WalletId: wallet.Id,
            OrganizationId: request.OrganizationId,
            AvailableBalance: wallet.AvailableBalance,
            LedgerBalance: wallet.AvailableBalance,
            Currency: wallet.Currency.ToString(),
            Status: wallet.Status.ToString(),
            AccountNumber: accountNumber,
            AccountName: accountName,
            BankName: bankName,
            BankCode: bankCode);
    }
}
