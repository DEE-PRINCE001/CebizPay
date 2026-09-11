using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Individuals;

/// <summary>
/// Query to retrieve a paginated directory of platform individual users with filtering and search.
/// </summary>
public sealed record GetAdminIndividualsDirectoryQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Search = null,
    string? Status = null,
    string? ProfessionalStatus = null) : IRequest<PagedResult<AdminIndividualSummaryDto>>;

/// <summary>
/// Validator for GetAdminIndividualsDirectoryQuery.
/// </summary>
public sealed class GetAdminIndividualsDirectoryQueryValidator : AbstractValidator<GetAdminIndividualsDirectoryQuery>
{
    /// <summary>
    /// Initializes validation rules for GetAdminIndividualsDirectoryQuery.
    /// </summary>
    public GetAdminIndividualsDirectoryQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetAdminIndividualsDirectoryQuery.
/// </summary>
public sealed class GetAdminIndividualsDirectoryQueryHandler : IRequestHandler<GetAdminIndividualsDirectoryQuery, PagedResult<AdminIndividualSummaryDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IIdentityService _identityService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminIndividualsDirectoryQueryHandler"/>.
    /// </summary>
    public GetAdminIndividualsDirectoryQueryHandler(
        IApplicationDbContext dbContext,
        IIdentityService identityService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AdminIndividualSummaryDto>> Handle(
        GetAdminIndividualsDirectoryQuery request,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.IndividualProfiles.AsQueryable();

        // 1. Filter by Search (name, email, phone number)
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

        // 2. Filter by ProfessionalStatus
        if (!string.IsNullOrWhiteSpace(request.ProfessionalStatus))
        {
            var profStatus = request.ProfessionalStatus.Trim();
            if (profStatus.Equals("Staff", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.ProfessionalStatus == ProfessionalStatus.Staff);
            }
            else if (profStatus.Equals("Not-a-Staff", StringComparison.OrdinalIgnoreCase) ||
                     profStatus.Equals("NotAStaff", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.ProfessionalStatus == ProfessionalStatus.NotAStaff);
            }
        }

        // 3. Filter by Status (Pending, Verified, Suspended, Rejected)
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

        // Retrieve identity metadata with lockout
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

        var items = new List<AdminIndividualSummaryDto>(profiles.Count);
        foreach (var profile in profiles)
        {
            userDetailsMap.TryGetValue(profile.UserId, out var details);

            var isSuspended = details.IsLockedOut;
            var displayStatus = isSuspended ? "Suspended" : profile.KycStatus.ToString();
            var companyName = userCompanyMap.GetValueOrDefault(profile.UserId, "None");
            var professionalStatusStr = profile.ProfessionalStatus == ProfessionalStatus.Staff ? "Staff" : "Not-a-Staff";
            var fullName = $"{profile.FirstName} {profile.LastName}".Trim();

            items.Add(new AdminIndividualSummaryDto(
                profile.Id,
                fullName,
                details.Email ?? string.Empty,
                details.PhoneNumber ?? string.Empty,
                professionalStatusStr,
                companyName,
                displayStatus,
                profile.AvatarUrl,
                profile.CreatedAtUtc));
        }

        return new PagedResult<AdminIndividualSummaryDto>(items, totalCount, request.PageNumber, request.PageSize);
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

        return null;
    }
}
