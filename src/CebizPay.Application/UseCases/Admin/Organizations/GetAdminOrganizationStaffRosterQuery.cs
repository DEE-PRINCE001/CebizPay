using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Organizations;

/// <summary>
/// Query to retrieve staff members associated with a specific organization when viewed by a platform administrator.
/// </summary>
public sealed record GetAdminOrganizationStaffRosterQuery(
    Guid OrganizationId,
    int PageNumber = 1,
    int PageSize = 10,
    string? Search = null) : IRequest<PagedResult<AdminStaffRosterItemDto>>;

/// <summary>
/// Validator for GetAdminOrganizationStaffRosterQuery.
/// </summary>
public sealed class GetAdminOrganizationStaffRosterQueryValidator : AbstractValidator<GetAdminOrganizationStaffRosterQuery>
{
    /// <summary>
    /// Initializes validation rules for GetAdminOrganizationStaffRosterQuery.
    /// </summary>
    public GetAdminOrganizationStaffRosterQueryValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID must not be empty.");

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetAdminOrganizationStaffRosterQuery.
/// </summary>
public sealed class GetAdminOrganizationStaffRosterQueryHandler : IRequestHandler<GetAdminOrganizationStaffRosterQuery, PagedResult<AdminStaffRosterItemDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IIdentityService _identityService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminOrganizationStaffRosterQueryHandler"/>.
    /// </summary>
    public GetAdminOrganizationStaffRosterQueryHandler(
        IApplicationDbContext dbContext,
        IIdentityService identityService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AdminStaffRosterItemDto>> Handle(GetAdminOrganizationStaffRosterQuery request, CancellationToken cancellationToken)
    {
        var orgExists = await _dbContext.Organizations
            .AnyAsync(o => o.Id == request.OrganizationId && !o.IsDeleted, cancellationToken);

        if (!orgExists)
        {
            throw new KeyNotFoundException($"Organization with ID '{request.OrganizationId}' was not found.");
        }

        var memberships = await _dbContext.OrganizationMemberships
            .Where(m => m.OrganizationId == request.OrganizationId)
            .OrderByDescending(m => m.JoinedAtUtc)
            .ToListAsync(cancellationToken);

        var userIds = memberships.Select(m => m.UserId).Distinct().ToList();

        var profilesList = await _dbContext.IndividualProfiles
            .Where(p => userIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);
        var profiles = profilesList.ToDictionary(p => p.UserId, p => p);

        var userDetails = await _identityService.GetUserDetailsByIdsAsync(userIds, cancellationToken);

        var walletsList = await _dbContext.Wallets
            .Where(w => w.IndividualId != null && userIds.Contains(w.IndividualId))
            .ToListAsync(cancellationToken);
        var wallets = walletsList
            .GroupBy(w => w.IndividualId!)
            .ToDictionary(g => g.Key, g => g.First());

        var virtualAccountsList = await _dbContext.VirtualAccounts
            .Where(va => va.IndividualId != null && userIds.Contains(va.IndividualId))
            .ToListAsync(cancellationToken);
        var virtualAccounts = virtualAccountsList
            .GroupBy(va => va.IndividualId!)
            .ToDictionary(g => g.Key, g => g.First());

        var levelIds = memberships
            .Where(m => m.SalaryLevelId.HasValue)
            .Select(m => m.SalaryLevelId!.Value)
            .Distinct()
            .ToList();

        var levelsList = await _dbContext.SalaryLevels
            .Where(s => s.OrganizationId == request.OrganizationId && levelIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
        var salaryLevels = levelsList.ToDictionary(s => s.Id, s => s);

        var allItems = memberships.Select(m =>
        {
            profiles.TryGetValue(m.UserId, out var prof);
            userDetails.TryGetValue(m.UserId, out var userDet);
            wallets.TryGetValue(m.UserId, out var wallet);
            virtualAccounts.TryGetValue(m.UserId, out var va);
            SalaryLevel? sl = null;
            if (m.SalaryLevelId.HasValue)
            {
                salaryLevels.TryGetValue(m.SalaryLevelId.Value, out sl);
            }

            var fullName = $"{prof?.FirstName} {prof?.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = userDet.Email ?? "Unknown";
            }

            var bankAccount = va != null
                ? $"{va.AccountNumber} {va.BankName}".Trim()
                : null;

            var status = prof?.KycStatus == KycStatus.Verified
                ? "Verified"
                : m.Status.ToString();

            return new AdminStaffRosterItemDto(
                m.Id.ToString(),
                fullName,
                wallet?.Id.ToString(),
                bankAccount,
                userDet.Email,
                sl?.BaseAmount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture),
                status,
                null);
        });

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            allItems = allItems.Where(i =>
                (!string.IsNullOrEmpty(i.Name) && i.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(i.Email) && i.Email.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(i.WalletId) && i.WalletId.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        var filteredList = allItems.ToList();
        var totalCount = filteredList.Count;

        var pagedItems = filteredList
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PagedResult<AdminStaffRosterItemDto>(pagedItems, totalCount, request.PageNumber, request.PageSize);
    }
}
