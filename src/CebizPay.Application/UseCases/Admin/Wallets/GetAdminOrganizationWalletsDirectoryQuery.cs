using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Loans.Enums;
using CebizPay.Domain.Payroll.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Wallets;

/// <summary>
/// Data transfer object representing a corporate organization wallet summary in the admin directory.
/// </summary>
public sealed record AdminOrganizationWalletSummaryDto(
    Guid Id,
    string Name,
    string? LogoUrl,
    decimal CurrentBalance,
    decimal TotalSalaryPaid,
    decimal TotalLoanPaid,
    string Currency,
    string Status);

/// <summary>
/// Query to retrieve a paginated directory of platform corporate organization wallets with financial totals.
/// </summary>
public sealed record GetAdminOrganizationWalletsDirectoryQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Search = null,
    string? Status = null) : IRequest<PagedResult<AdminOrganizationWalletSummaryDto>>;

/// <summary>
/// Validator for <see cref="GetAdminOrganizationWalletsDirectoryQuery"/>.
/// </summary>
public sealed class GetAdminOrganizationWalletsDirectoryQueryValidator : AbstractValidator<GetAdminOrganizationWalletsDirectoryQuery>
{
    /// <summary>
    /// Initializes validation rules for GetAdminOrganizationWalletsDirectoryQuery.
    /// </summary>
    public GetAdminOrganizationWalletsDirectoryQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for <see cref="GetAdminOrganizationWalletsDirectoryQuery"/>.
/// </summary>
public sealed class GetAdminOrganizationWalletsDirectoryQueryHandler : IRequestHandler<GetAdminOrganizationWalletsDirectoryQuery, PagedResult<AdminOrganizationWalletSummaryDto>>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminOrganizationWalletsDirectoryQueryHandler"/>.
    /// </summary>
    public GetAdminOrganizationWalletsDirectoryQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AdminOrganizationWalletSummaryDto>> Handle(
        GetAdminOrganizationWalletsDirectoryQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Organizations.Where(o => !o.IsDeleted);

        // 1. Search filter by company name or registration number
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(o =>
                o.CompanyName.ToLower().Contains(search) ||
                (o.CacNumber != null && o.CacNumber.ToLower().Contains(search)));
#pragma warning restore CA1862, CA1304, CA1311
        }

        // 2. Status filter
        var parsedStatus = ParseStatus(request.Status);
        if (parsedStatus.HasValue)
        {
            query = query.Where(o => o.Status == parsedStatus.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var organizations = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var orgIds = organizations.Select(o => o.Id).ToList();

        // 3. Batch fetch primary NGN wallets to prevent N+1 queries
        var wallets = await _dbContext.Wallets
            .Where(w => w.OrganizationId.HasValue && orgIds.Contains(w.OrganizationId.Value) && w.Currency == Currency.NGN)
            .ToListAsync(cancellationToken);
        var balanceMap = wallets
            .Where(w => w.OrganizationId.HasValue)
            .GroupBy(w => w.OrganizationId!.Value)
            .ToDictionary(g => g.Key, g => g.First().AvailableBalance);

        // 4. Batch fetch completed payroll items for total salary paid
        var completedSalaryItems = await _dbContext.PayrollItems
            .Where(p => orgIds.Contains(p.OrganizationId) && p.Status == PayrollItemStatus.Completed && p.Currency == Currency.NGN)
            .ToListAsync(cancellationToken);
        var salaryPaidMap = completedSalaryItems
            .GroupBy(p => p.OrganizationId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.NetPay));

        // 5. Batch fetch loan disbursements for total corporate loan disbursements
        var loanContracts = await _dbContext.LoanContracts
            .Where(l => orgIds.Contains(l.OrganizationId) && l.Status != LoanContractStatus.Cancelled)
            .ToListAsync(cancellationToken);
        var loanPaidMap = loanContracts
            .GroupBy(l => l.OrganizationId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.OriginalPrincipal));

        var items = new List<AdminOrganizationWalletSummaryDto>(organizations.Count);
        foreach (var org in organizations)
        {
            var balance = balanceMap.GetValueOrDefault(org.Id, 0m);
            var salaryPaid = salaryPaidMap.GetValueOrDefault(org.Id, 0m);
            var loanPaid = loanPaidMap.GetValueOrDefault(org.Id, 0m);

            items.Add(new AdminOrganizationWalletSummaryDto(
                org.Id,
                org.CompanyName,
                org.LogoUrl,
                balance,
                salaryPaid,
                loanPaid,
                "NGN",
                org.Status.ToString()));
        }

        return new PagedResult<AdminOrganizationWalletSummaryDto>(items, totalCount, request.PageNumber, request.PageSize);
    }

    internal static OrganizationStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var trimmed = status.Trim();

        if (int.TryParse(trimmed, out var intStatus) && Enum.IsDefined(typeof(OrganizationStatus), intStatus))
        {
            return (OrganizationStatus)intStatus;
        }

        if (Enum.TryParse<OrganizationStatus>(trimmed, ignoreCase: true, out var parsedStatus))
        {
            return parsedStatus;
        }

        return null;
    }
}
