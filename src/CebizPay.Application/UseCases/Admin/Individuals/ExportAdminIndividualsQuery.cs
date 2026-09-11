using System.Globalization;
using System.Text;
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Individuals;

/// <summary>
/// Query to export filtered individuals dataset directly as a downloadable CSV file.
/// </summary>
public sealed record ExportAdminIndividualsQuery(
    string? Search = null,
    string? Status = null,
    string Format = "csv") : IRequest<ExportAdminIndividualsResult>;

/// <summary>
/// Handler for <see cref="ExportAdminIndividualsQuery"/>.
/// </summary>
public sealed class ExportAdminIndividualsQueryHandler : IRequestHandler<ExportAdminIndividualsQuery, ExportAdminIndividualsResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IIdentityService _identityService;

    /// <summary>
    /// Initializes a new instance of <see cref="ExportAdminIndividualsQueryHandler"/>.
    /// </summary>
    public ExportAdminIndividualsQueryHandler(
        IApplicationDbContext dbContext,
        IIdentityService identityService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
    }

    /// <inheritdoc/>
    public async Task<ExportAdminIndividualsResult> Handle(
        ExportAdminIndividualsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.IndividualProfiles.AsQueryable();

        // 1. Filter by Search
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            var matchedUserIds = await _identityService.SearchUserIdsAsync(search, cancellationToken);

#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(p =>
                p.FirstName.ToLower().Contains(search) ||
                p.LastName.ToLower().Contains(search) ||
                (p.MiddleName != null && p.MiddleName.ToLower().Contains(search)) ||
                matchedUserIds.Contains(p.UserId));
#pragma warning restore CA1862, CA1304, CA1311
        }

        // 2. Filter by Status
        var parsedStatus = GetAdminIndividualsDirectoryQueryHandler.ParseStatus(request.Status);
        if (parsedStatus.HasValue)
        {
            query = query.Where(p => p.KycStatus == parsedStatus.Value);
        }

        var profiles = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var userIds = profiles.Select(p => p.UserId).Distinct().ToList();

        // Retrieve identity metadata
        var userDetailsMap = await _identityService.GetUserDetailsWithLockoutByIdsAsync(userIds, cancellationToken);

        // Retrieve active organization memberships for employer company name
        var activeMemberships = await _dbContext.OrganizationMemberships
            .Where(m => userIds.Contains(m.UserId) && m.Status == MembershipStatus.Active)
            .ToListAsync(cancellationToken);

        var orgIds = activeMemberships.Select(m => m.OrganizationId).Distinct().ToList();
        var orgs = await _dbContext.Organizations
            .Where(o => orgIds.Contains(o.Id))
            .ToListAsync(cancellationToken);

        var orgNameMap = orgs.ToDictionary(o => o.Id, o => o.CompanyName);
        var userCompanyMap = activeMemberships
            .GroupBy(m => m.UserId)
            .ToDictionary(
                g => g.Key,
                g => orgNameMap.GetValueOrDefault(g.First().OrganizationId, "None"));

        var sb = new StringBuilder();
        sb.AppendLine("Individual ID,Full Name,Email,Phone Number,Professional Status,Company Name,Status,Created At (UTC)");

        foreach (var profile in profiles)
        {
            userDetailsMap.TryGetValue(profile.UserId, out var details);

            var isSuspended = details.IsLockedOut;
            var displayStatus = isSuspended ? "Suspended" : profile.KycStatus.ToString();
            var companyName = userCompanyMap.GetValueOrDefault(profile.UserId, "None");
            var professionalStatusStr = profile.ProfessionalStatus == ProfessionalStatus.Staff ? "Staff" : "Not-a-Staff";
            var fullName = $"{profile.FirstName} {profile.LastName}".Trim();

            sb.Append(EscapeCsv(profile.Id.ToString())).Append(',');
            sb.Append(EscapeCsv(fullName)).Append(',');
            sb.Append(EscapeCsv(details.Email ?? string.Empty)).Append(',');
            sb.Append(EscapeCsv(details.PhoneNumber ?? string.Empty)).Append(',');
            sb.Append(EscapeCsv(professionalStatusStr)).Append(',');
            sb.Append(EscapeCsv(companyName)).Append(',');
            sb.Append(EscapeCsv(displayStatus)).Append(',');
            sb.AppendLine(EscapeCsv(profile.CreatedAtUtc.ToString("o", CultureInfo.InvariantCulture)));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());

        return new ExportAdminIndividualsResult(
            bytes,
            "text/csv; charset=utf-8",
            "individuals_export.csv");
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"");
        if (escaped.StartsWith('=') || escaped.StartsWith('+') || escaped.StartsWith('-') || escaped.StartsWith('@'))
        {
            escaped = "'" + escaped;
        }

        if (escaped.Contains(',') || escaped.Contains('"') || escaped.Contains('\n') || escaped.Contains('\r'))
        {
            return $"\"{escaped}\"";
        }

        return escaped;
    }
}
