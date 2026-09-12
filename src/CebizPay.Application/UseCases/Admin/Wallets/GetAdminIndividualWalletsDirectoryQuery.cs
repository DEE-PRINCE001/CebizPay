using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Loans.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Wallets;

/// <summary>
/// Data transfer object representing an individual user wallet summary in the admin directory.
/// </summary>
public sealed record AdminIndividualWalletSummaryDto(
    Guid Id,
    string Name,
    string? AvatarUrl,
    decimal CurrentBalance,
    decimal LoanRepayable,
    string Currency,
    string Status);

/// <summary>
/// Query to retrieve a paginated directory of platform individual user wallets with balance and outstanding loan obligation.
/// </summary>
public sealed record GetAdminIndividualWalletsDirectoryQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Search = null,
    string? Status = null) : IRequest<PagedResult<AdminIndividualWalletSummaryDto>>;

/// <summary>
/// Validator for <see cref="GetAdminIndividualWalletsDirectoryQuery"/>.
/// </summary>
public sealed class GetAdminIndividualWalletsDirectoryQueryValidator : AbstractValidator<GetAdminIndividualWalletsDirectoryQuery>
{
    /// <summary>
    /// Initializes validation rules for GetAdminIndividualWalletsDirectoryQuery.
    /// </summary>
    public GetAdminIndividualWalletsDirectoryQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for <see cref="GetAdminIndividualWalletsDirectoryQuery"/>.
/// </summary>
public sealed class GetAdminIndividualWalletsDirectoryQueryHandler : IRequestHandler<GetAdminIndividualWalletsDirectoryQuery, PagedResult<AdminIndividualWalletSummaryDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IIdentityService _identityService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminIndividualWalletsDirectoryQueryHandler"/>.
    /// </summary>
    public GetAdminIndividualWalletsDirectoryQueryHandler(
        IApplicationDbContext dbContext,
        IIdentityService identityService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AdminIndividualWalletSummaryDto>> Handle(
        GetAdminIndividualWalletsDirectoryQuery request,
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
        var parsedStatus = ParseStatus(request.Status);
        if (parsedStatus.HasValue)
        {
            query = query.Where(p => p.KycStatus == parsedStatus.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var profiles = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var userIds = profiles.Select(p => p.UserId).Distinct().ToList();

        // 3. Identity lockout resolution for suspended status
        var userDetailsMap = await _identityService.GetUserDetailsWithLockoutByIdsAsync(userIds, cancellationToken);

        // 4. Batch query primary NGN wallets
        var wallets = await _dbContext.Wallets
            .Where(w => w.IndividualId != null && userIds.Contains(w.IndividualId) && w.Currency == Currency.NGN)
            .ToListAsync(cancellationToken);
        var balanceMap = wallets
            .Where(w => w.IndividualId != null)
            .GroupBy(w => w.IndividualId!)
            .ToDictionary(g => g.Key, g => g.First().AvailableBalance);

        // 5. Batch query active loan contracts for total outstanding loan repayable
        var activeLoans = await _dbContext.LoanContracts
            .Where(l => userIds.Contains(l.BorrowerUserId) &&
                        (l.Status == LoanContractStatus.Active || l.Status == LoanContractStatus.Overdue))
            .ToListAsync(cancellationToken);
        var loanRepayableMap = activeLoans
            .GroupBy(l => l.BorrowerUserId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.OutstandingPrincipal));

        var items = new List<AdminIndividualWalletSummaryDto>(profiles.Count);
        foreach (var profile in profiles)
        {
            userDetailsMap.TryGetValue(profile.UserId, out var details);

            var isSuspended = details.IsLockedOut;
            var displayStatus = isSuspended ? "Suspended" : (profile.KycStatus == KycStatus.Verified ? "Active" : profile.KycStatus.ToString());
            var fullName = $"{profile.FirstName} {profile.LastName}".Trim();
            var currentBalance = balanceMap.GetValueOrDefault(profile.UserId, 0m);
            var loanRepayable = loanRepayableMap.GetValueOrDefault(profile.UserId, 0m);

            items.Add(new AdminIndividualWalletSummaryDto(
                profile.Id,
                fullName,
                profile.AvatarUrl,
                currentBalance,
                loanRepayable,
                "NGN",
                displayStatus));
        }

        return new PagedResult<AdminIndividualWalletSummaryDto>(items, totalCount, request.PageNumber, request.PageSize);
    }

    internal static KycStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var trimmed = status.Trim();

        if (int.TryParse(trimmed, out var intStatus) && Enum.IsDefined(typeof(KycStatus), intStatus))
        {
            return (KycStatus)intStatus;
        }

        if (Enum.TryParse<KycStatus>(trimmed, ignoreCase: true, out var parsedStatus))
        {
            return parsedStatus;
        }

        if (trimmed.Equals("Active", StringComparison.OrdinalIgnoreCase))
        {
            return KycStatus.Verified;
        }

        return null;
    }
}
