using System.Globalization;
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payroll.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Organizations;

/// <summary>
/// Data transfer object representing a salary disbursement line item in the admin view.
/// </summary>
public sealed record AdminOrganizationSalaryItemDto(
    string Id,
    decimal Amount,
    string TransactionId,
    string Method,
    string AccountOrWalletId,
    string Month,
    DateTime DateTime,
    string Status);

/// <summary>
/// Query to retrieve a paginated list of salary disbursement line items for an organization.
/// </summary>
public sealed record GetAdminOrganizationSalariesQuery(
    Guid OrganizationId,
    int PageNumber = 1,
    int PageSize = 10,
    string? Search = null,
    string? Month = null,
    string? Status = null) : IRequest<PagedResult<AdminOrganizationSalaryItemDto>>;

/// <summary>
/// Validator for <see cref="GetAdminOrganizationSalariesQuery"/>.
/// </summary>
public sealed class GetAdminOrganizationSalariesQueryValidator : AbstractValidator<GetAdminOrganizationSalariesQuery>
{
    /// <summary>
    /// Initializes validation rules for GetAdminOrganizationSalariesQuery.
    /// </summary>
    public GetAdminOrganizationSalariesQueryValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("OrganizationId is required.");

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for <see cref="GetAdminOrganizationSalariesQuery"/>.
/// </summary>
public sealed class GetAdminOrganizationSalariesQueryHandler : IRequestHandler<GetAdminOrganizationSalariesQuery, PagedResult<AdminOrganizationSalaryItemDto>>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminOrganizationSalariesQueryHandler"/>.
    /// </summary>
    public GetAdminOrganizationSalariesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AdminOrganizationSalaryItemDto>> Handle(
        GetAdminOrganizationSalariesQuery request,
        CancellationToken cancellationToken)
    {
        var org = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId && !o.IsDeleted, cancellationToken);

        if (org == null)
        {
            throw new KeyNotFoundException($"Organization '{request.OrganizationId}' was not found.");
        }

        var query = _dbContext.PayrollItems
            .Where(p => p.OrganizationId == request.OrganizationId);

        // 1. Search filter: employee name, email, user ID, or transaction ID
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(p =>
                p.EmployeeName.ToLower().Contains(search) ||
                p.EmployeeEmail.ToLower().Contains(search) ||
                p.EmployeeUserId.ToLower().Contains(search) ||
                (p.LedgerTransactionId != null && p.LedgerTransactionId.Value.ToString().ToLower().Contains(search)));
#pragma warning restore CA1862, CA1304, CA1311
        }

        // 2. Status filter
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var statusStr = request.Status.Trim();
            if (statusStr.Equals("Successfull", StringComparison.OrdinalIgnoreCase) ||
                statusStr.Equals("Successful", StringComparison.OrdinalIgnoreCase) ||
                statusStr.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.Status == PayrollItemStatus.Completed);
            }
            else if (statusStr.Equals("Failed", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.Status == PayrollItemStatus.Failed);
            }
            else if (statusStr.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.Status == PayrollItemStatus.Pending ||
                                         p.Status == PayrollItemStatus.Processing ||
                                         p.Status == PayrollItemStatus.RetryPending);
            }
        }

        // 3. Month filter
        int? filterMonth = null;
        if (!string.IsNullOrWhiteSpace(request.Month))
        {
            filterMonth = ParseMonth(request.Month);
            if (filterMonth.HasValue)
            {
                query = query.Where(p => p.CreatedAtUtc.Month == filterMonth.Value);
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var batchIds = items.Select(i => i.PayrollBatchId).Distinct().ToList();
        var batches = await _dbContext.PayrollBatches
            .Where(b => batchIds.Contains(b.Id))
            .ToListAsync(cancellationToken);
        var batchMap = batches.ToDictionary(b => b.Id);

        var employeeUserIds = items.Select(i => i.EmployeeUserId).Distinct().ToList();
        var wallets = await _dbContext.Wallets
            .Where(w => w.IndividualId != null && employeeUserIds.Contains(w.IndividualId) && w.Currency == Currency.NGN)
            .ToListAsync(cancellationToken);
        var walletMap = wallets
            .Where(w => w.IndividualId != null)
            .GroupBy(w => w.IndividualId!)
            .ToDictionary(g => g.Key, g => g.First());

        var resultItems = new List<AdminOrganizationSalaryItemDto>(items.Count);
        foreach (var item in items)
        {
            batchMap.TryGetValue(item.PayrollBatchId, out var batch);
            walletMap.TryGetValue(item.EmployeeUserId, out var wallet);

            var monthName = batch != null
                ? batch.PeriodStart.ToString("MMMM", CultureInfo.InvariantCulture)
                : item.CreatedAtUtc.ToString("MMMM", CultureInfo.InvariantCulture);

            var accountOrWalletId = wallet != null
                ? wallet.Id.ToString("N")[..12]
                : item.EmployeeUserId;

            var transactionIdStr = item.LedgerTransactionId.HasValue
                ? item.LedgerTransactionId.Value.ToString("N")[..13]
                : item.Id.ToString("N")[..13];

            var statusDisplay = item.Status == PayrollItemStatus.Completed
                ? "Successfull"
                : (item.Status == PayrollItemStatus.Failed ? "Failed" : "Pending");

            resultItems.Add(new AdminOrganizationSalaryItemDto(
                $"sal-{item.Id.ToString("N")[..10]}",
                item.NetPay,
                transactionIdStr,
                "Wallet ID",
                accountOrWalletId,
                monthName,
                item.CreatedAtUtc,
                statusDisplay));
        }

        return new PagedResult<AdminOrganizationSalaryItemDto>(resultItems, totalCount, request.PageNumber, request.PageSize);
    }

    internal static int? ParseMonth(string? month)
    {
        if (string.IsNullOrWhiteSpace(month))
        {
            return null;
        }

        var trimmed = month.Trim();

        if (int.TryParse(trimmed, out var intMonth) && intMonth >= 1 && intMonth <= 12)
        {
            return intMonth;
        }

        if (DateTime.TryParseExact(trimmed, "MMMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
            DateTime.TryParseExact(trimmed, "MMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return date.Month;
        }

        return null;
    }
}
