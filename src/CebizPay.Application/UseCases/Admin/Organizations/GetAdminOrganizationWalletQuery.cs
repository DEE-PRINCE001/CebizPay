using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Loans.Enums;
using CebizPay.Domain.Payroll.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Organizations;

/// <summary>
/// Data transfer object representing corporate organization wallet overview in admin scope.
/// </summary>
public sealed record AdminOrganizationWalletDetailsDto(
    Guid OrganizationId,
    string WalletId,
    string Currency,
    decimal CurrentBalance,
    decimal TotalSalaryPaid,
    decimal TotalLoanFund,
    string VirtualAccountNumber,
    string BankName,
    string Status);

/// <summary>
/// Query to retrieve specific corporate organization wallet overview and cumulative disbursement metrics.
/// </summary>
public sealed record GetAdminOrganizationWalletQuery(Guid Id) : IRequest<AdminOrganizationWalletDetailsDto?>;

/// <summary>
/// Handler for <see cref="GetAdminOrganizationWalletQuery"/>.
/// </summary>
public sealed class GetAdminOrganizationWalletQueryHandler : IRequestHandler<GetAdminOrganizationWalletQuery, AdminOrganizationWalletDetailsDto?>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminOrganizationWalletQueryHandler"/>.
    /// </summary>
    public GetAdminOrganizationWalletQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<AdminOrganizationWalletDetailsDto?> Handle(
        GetAdminOrganizationWalletQuery request,
        CancellationToken cancellationToken)
    {
        var org = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.Id && !o.IsDeleted, cancellationToken);

        if (org == null)
        {
            return null;
        }

        // 1. Resolve primary NGN wallet
        var wallet = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.OrganizationId == org.Id && w.Currency == Currency.NGN, cancellationToken);

        // 2. Resolve virtual account
        var virtualAccount = await _dbContext.VirtualAccounts
            .FirstOrDefaultAsync(v => v.OrganizationId == org.Id && v.Currency == Currency.NGN, cancellationToken);

        // 3. Compute total salary disbursements
        var completedSalaryItems = await _dbContext.PayrollItems
            .Where(p => p.OrganizationId == org.Id && p.Status == PayrollItemStatus.Completed && p.Currency == Currency.NGN)
            .ToListAsync(cancellationToken);
        var totalSalaryPaid = completedSalaryItems.Sum(p => p.NetPay);

        // 4. Compute total corporate loan funds disbursed
        var activeLoans = await _dbContext.LoanContracts
            .Where(l => l.OrganizationId == org.Id && l.Status != LoanContractStatus.Cancelled)
            .ToListAsync(cancellationToken);
        var totalLoanFund = activeLoans.Sum(l => l.OriginalPrincipal);

        var walletIdStr = wallet != null
            ? $"WAL-ORG-{wallet.Id.ToString("N")[..8].ToUpperInvariant()}"
            : $"WAL-ORG-{org.Id.ToString("N")[..8].ToUpperInvariant()}";

        var currentBalance = wallet?.AvailableBalance ?? 0m;
        var virtualAccNumber = virtualAccount?.AccountNumber ?? "0123456789";
        var bankName = virtualAccount?.BankName ?? "Wema Bank / CebizPay";
        var statusStr = wallet?.Status.ToString() ?? org.Status.ToString();

        return new AdminOrganizationWalletDetailsDto(
            org.Id,
            walletIdStr,
            "NGN",
            currentBalance,
            totalSalaryPaid,
            totalLoanFund,
            virtualAccNumber,
            bankName,
            statusStr);
    }
}
