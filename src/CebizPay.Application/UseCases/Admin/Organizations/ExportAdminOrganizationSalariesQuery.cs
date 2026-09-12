using System.Globalization;
using System.Text;
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payroll.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Organizations;

/// <summary>
/// Query to export salary disbursements for an organization as a downloadable CSV stream.
/// </summary>
public sealed record ExportAdminOrganizationSalariesQuery(
    Guid OrganizationId,
    string? Search = null,
    string? Month = null,
    string? Status = null,
    string Format = "csv") : IRequest<ExportAdminOrganizationSalariesResult>;

/// <summary>
/// Result container for exported organization salaries file.
/// </summary>
public sealed record ExportAdminOrganizationSalariesResult(
    byte[] Content,
    string ContentType,
    string FileName);

/// <summary>
/// Handler for <see cref="ExportAdminOrganizationSalariesQuery"/>.
/// </summary>
public sealed class ExportAdminOrganizationSalariesQueryHandler : IRequestHandler<ExportAdminOrganizationSalariesQuery, ExportAdminOrganizationSalariesResult>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="ExportAdminOrganizationSalariesQueryHandler"/>.
    /// </summary>
    public ExportAdminOrganizationSalariesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<ExportAdminOrganizationSalariesResult> Handle(
        ExportAdminOrganizationSalariesQuery request,
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

        // 1. Search filter
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
        if (!string.IsNullOrWhiteSpace(request.Month))
        {
            var filterMonth = GetAdminOrganizationSalariesQueryHandler.ParseMonth(request.Month);
            if (filterMonth.HasValue)
            {
                query = query.Where(p => p.CreatedAtUtc.Month == filterMonth.Value);
            }
        }

        var items = await query
            .OrderByDescending(p => p.CreatedAtUtc)
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

        var sb = new StringBuilder();
        sb.AppendLine("Disbursement ID,Amount (NGN),Transaction ID,Method,Account or Wallet ID,Month,Date Time (UTC),Status");

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

            sb.Append(EscapeCsv($"sal-{item.Id.ToString("N")[..10]}")).Append(',');
            sb.Append(item.NetPay.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(transactionIdStr)).Append(',');
            sb.Append("Wallet ID,");
            sb.Append(EscapeCsv(accountOrWalletId)).Append(',');
            sb.Append(EscapeCsv(monthName)).Append(',');
            sb.Append(EscapeCsv(item.CreatedAtUtc.ToString("o", CultureInfo.InvariantCulture))).Append(',');
            sb.AppendLine(EscapeCsv(statusDisplay));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());

        return new ExportAdminOrganizationSalariesResult(
            bytes,
            "text/csv; charset=utf-8",
            $"organization_{request.OrganizationId}_salaries_export.csv");
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',', StringComparison.Ordinal) ||
            value.Contains('"', StringComparison.Ordinal) ||
            value.Contains('\n', StringComparison.Ordinal) ||
            value.Contains('\r', StringComparison.Ordinal))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}
