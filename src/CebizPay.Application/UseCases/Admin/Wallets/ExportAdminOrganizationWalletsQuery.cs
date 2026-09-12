using System.Globalization;
using System.Text;
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Loans.Enums;
using CebizPay.Domain.Payroll.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Wallets;

/// <summary>
/// Query to export corporate organization wallets directory as a downloadable CSV stream.
/// </summary>
public sealed record ExportAdminOrganizationWalletsQuery(
    string? Search = null,
    string? Status = null,
    string Format = "csv") : IRequest<ExportAdminOrganizationWalletsResult>;

/// <summary>
/// Result container for exported corporate organization wallets file.
/// </summary>
public sealed record ExportAdminOrganizationWalletsResult(
    byte[] Content,
    string ContentType,
    string FileName);

/// <summary>
/// Handler for <see cref="ExportAdminOrganizationWalletsQuery"/>.
/// </summary>
public sealed class ExportAdminOrganizationWalletsQueryHandler : IRequestHandler<ExportAdminOrganizationWalletsQuery, ExportAdminOrganizationWalletsResult>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="ExportAdminOrganizationWalletsQueryHandler"/>.
    /// </summary>
    public ExportAdminOrganizationWalletsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<ExportAdminOrganizationWalletsResult> Handle(
        ExportAdminOrganizationWalletsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Organizations.Where(o => !o.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(o =>
                o.CompanyName.ToLower().Contains(search) ||
                (o.CacNumber != null && o.CacNumber.ToLower().Contains(search)));
#pragma warning restore CA1862, CA1304, CA1311
        }

        var parsedStatus = GetAdminOrganizationWalletsDirectoryQueryHandler.ParseStatus(request.Status);
        if (parsedStatus.HasValue)
        {
            query = query.Where(o => o.Status == parsedStatus.Value);
        }

        var organizations = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var orgIds = organizations.Select(o => o.Id).ToList();

        // Batch fetch balances
        var wallets = await _dbContext.Wallets
            .Where(w => w.OrganizationId.HasValue && orgIds.Contains(w.OrganizationId.Value) && w.Currency == Currency.NGN)
            .ToListAsync(cancellationToken);
        var balanceMap = wallets
            .Where(w => w.OrganizationId.HasValue)
            .GroupBy(w => w.OrganizationId!.Value)
            .ToDictionary(g => g.Key, g => g.First().AvailableBalance);

        // Batch fetch salaries paid
        var completedSalaryItems = await _dbContext.PayrollItems
            .Where(p => orgIds.Contains(p.OrganizationId) && p.Status == PayrollItemStatus.Completed && p.Currency == Currency.NGN)
            .ToListAsync(cancellationToken);
        var salaryPaidMap = completedSalaryItems
            .GroupBy(p => p.OrganizationId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.NetPay));

        // Batch fetch loan disbursements
        var loanContracts = await _dbContext.LoanContracts
            .Where(l => orgIds.Contains(l.OrganizationId) && l.Status != LoanContractStatus.Cancelled)
            .ToListAsync(cancellationToken);
        var loanPaidMap = loanContracts
            .GroupBy(l => l.OrganizationId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.OriginalPrincipal));

        var sb = new StringBuilder();
        sb.AppendLine("Organization ID,Company Name,Current Balance,Total Salary Paid,Total Loan Paid,Currency,Status");

        foreach (var org in organizations)
        {
            var balance = balanceMap.GetValueOrDefault(org.Id, 0m);
            var salaryPaid = salaryPaidMap.GetValueOrDefault(org.Id, 0m);
            var loanPaid = loanPaidMap.GetValueOrDefault(org.Id, 0m);

            sb.Append(EscapeCsv(org.Id.ToString())).Append(',');
            sb.Append(EscapeCsv(org.CompanyName)).Append(',');
            sb.Append(balance.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(salaryPaid.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(loanPaid.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append("NGN,");
            sb.AppendLine(EscapeCsv(org.Status.ToString()));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());

        return new ExportAdminOrganizationWalletsResult(
            bytes,
            "text/csv; charset=utf-8",
            "organization_wallets_export.csv");
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
