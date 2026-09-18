using System.Globalization;
using System.Text;
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Loans.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Wallets;

/// <summary>
/// Query to export individual user wallets directory as a downloadable CSV stream.
/// </summary>
public sealed record ExportAdminIndividualWalletsQuery(
    string? Search = null,
    string? Status = null,
    string Format = "csv") : IRequest<ExportAdminIndividualWalletsResult>;

/// <summary>
/// Result container for exported individual user wallets file.
/// </summary>
public sealed record ExportAdminIndividualWalletsResult(
    byte[] Content,
    string ContentType,
    string FileName);

/// <summary>
/// Handler for <see cref="ExportAdminIndividualWalletsQuery"/>.
/// </summary>
public sealed class ExportAdminIndividualWalletsQueryHandler : IRequestHandler<ExportAdminIndividualWalletsQuery, ExportAdminIndividualWalletsResult>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IIdentityService _identityService;

    /// <summary>
    /// Initializes a new instance of <see cref="ExportAdminIndividualWalletsQueryHandler"/>.
    /// </summary>
    public ExportAdminIndividualWalletsQueryHandler(
        IApplicationDbContext dbContext,
        IIdentityService identityService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
    }

    /// <inheritdoc/>
    public async Task<ExportAdminIndividualWalletsResult> Handle(
        ExportAdminIndividualWalletsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.IndividualProfiles.AsQueryable();

        // 1. Search by name or email
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

        // 2. Status filter
        if (string.Equals(request.Status?.Trim(), "Suspended", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(p => p.IsSuspended);
        }
        else
        {
            var parsedStatus = GetAdminIndividualWalletsDirectoryQueryHandler.ParseStatus(request.Status);
            if (parsedStatus.HasValue)
            {
                query = query.Where(p => !p.IsSuspended && p.KycStatus == parsedStatus.Value);
            }
        }

        var profiles = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var userIds = profiles.Select(p => p.UserId).Distinct().ToList();

        var userDetailsMap = await _identityService.GetUserDetailsWithLockoutByIdsAsync(userIds, cancellationToken);

        // Batch query primary NGN wallets
        var wallets = await _dbContext.Wallets
            .Where(w => w.IndividualId != null && userIds.Contains(w.IndividualId) && w.Currency == Currency.NGN)
            .ToListAsync(cancellationToken);
        var balanceMap = wallets
            .Where(w => w.IndividualId != null)
            .GroupBy(w => w.IndividualId!)
            .ToDictionary(g => g.Key, g => g.First().AvailableBalance);

        // Batch query active loan contracts
        var activeLoans = await _dbContext.LoanContracts
            .Where(l => userIds.Contains(l.BorrowerUserId) &&
                        (l.Status == LoanContractStatus.Active || l.Status == LoanContractStatus.Overdue))
            .ToListAsync(cancellationToken);
        var loanRepayableMap = activeLoans
            .GroupBy(l => l.BorrowerUserId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.OutstandingPrincipal));

        var sb = new StringBuilder();
        sb.AppendLine("Individual ID,Full Name,Current Balance,Loan Repayable,Currency,Status");

        foreach (var profile in profiles)
        {
            userDetailsMap.TryGetValue(profile.UserId, out var details);

            var isSuspended = profile.IsSuspended || details.IsLockedOut;
            var displayStatus = isSuspended ? "Suspended" : (profile.KycStatus == KycStatus.Verified ? "Active" : profile.KycStatus.ToString());
            var fullName = $"{profile.FirstName} {profile.LastName}".Trim();
            var currentBalance = balanceMap.GetValueOrDefault(profile.UserId, 0m);
            var loanRepayable = loanRepayableMap.GetValueOrDefault(profile.UserId, 0m);

            sb.Append(EscapeCsv(profile.Id.ToString())).Append(',');
            sb.Append(EscapeCsv(fullName)).Append(',');
            sb.Append(currentBalance.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(loanRepayable.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append("NGN,");
            sb.AppendLine(EscapeCsv(displayStatus));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());

        return new ExportAdminIndividualWalletsResult(
            bytes,
            "text/csv; charset=utf-8",
            "individual_wallets_export.csv");
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
