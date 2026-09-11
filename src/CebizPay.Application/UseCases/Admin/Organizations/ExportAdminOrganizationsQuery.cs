using System.Globalization;
using System.Text;
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Organizations;

/// <summary>
/// Query to export filtered organizations data as a downloadable file.
/// </summary>
public sealed record ExportAdminOrganizationsQuery(
    string? Search = null,
    string? Status = null,
    string Format = "csv") : IRequest<ExportAdminOrganizationsResult>;

/// <summary>
/// Handler for ExportAdminOrganizationsQuery.
/// </summary>
public sealed class ExportAdminOrganizationsQueryHandler : IRequestHandler<ExportAdminOrganizationsQuery, ExportAdminOrganizationsResult>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="ExportAdminOrganizationsQueryHandler"/>.
    /// </summary>
    public ExportAdminOrganizationsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<ExportAdminOrganizationsResult> Handle(ExportAdminOrganizationsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Organizations.Where(o => !o.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(o =>
                o.CompanyName.ToLower().Contains(search) ||
                o.Email.ToLower().Contains(search) ||
                (o.Address != null && o.Address.ToLower().Contains(search)));
#pragma warning restore CA1862, CA1304, CA1311
        }

        var parsedStatus = GetAdminOrganizationsDirectoryQueryHandler.ParseStatus(request.Status);
        if (parsedStatus.HasValue)
        {
            query = query.Where(o => o.Status == parsedStatus.Value);
        }

        var organizations = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var orgIds = organizations.Select(o => o.Id).ToList();

        var memberships = await _dbContext.OrganizationMemberships
            .Where(m => orgIds.Contains(m.OrganizationId) && m.Status != MembershipStatus.Terminated)
            .ToListAsync(cancellationToken);

        var staffCounts = memberships
            .GroupBy(m => m.OrganizationId)
            .ToDictionary(g => g.Key, g => g.Count());

        var sb = new StringBuilder();
        sb.AppendLine("Organization ID,Company Name,Category,Email,Phone,Address,Status,KYB Status,Staff Count,CAC Number,Created At (UTC)");

        foreach (var org in organizations)
        {
            var staffCount = staffCounts.GetValueOrDefault(org.Id, 0);
            sb.Append(EscapeCsv(org.Id.ToString())).Append(',');
            sb.Append(EscapeCsv(org.CompanyName)).Append(',');
            sb.Append(EscapeCsv(org.Category ?? string.Empty)).Append(',');
            sb.Append(EscapeCsv(org.Email)).Append(',');
            sb.Append(EscapeCsv(org.Phone)).Append(',');
            sb.Append(EscapeCsv(org.Address ?? string.Empty)).Append(',');
            sb.Append(EscapeCsv(org.Status.ToString())).Append(',');
            sb.Append(EscapeCsv(org.KybStatus.ToString())).Append(',');
            sb.Append(staffCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(org.CacNumber ?? string.Empty)).Append(',');
            sb.AppendLine(EscapeCsv(org.CreatedAtUtc.ToString("o", CultureInfo.InvariantCulture)));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());

        return new ExportAdminOrganizationsResult(
            bytes,
            "text/csv",
            "organizations_export.csv");
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
