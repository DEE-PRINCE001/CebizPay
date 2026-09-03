using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Communication.Enums;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Announcements;

/// <summary>
/// Query to retrieve a paginated directory of announcements for administrative management.
/// </summary>
public sealed record GetAnnouncementsDirectoryQuery(
    AnnouncementScope? Scope = null,
    AnnouncementStatus? Status = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<AnnouncementDto>>;

/// <summary>
/// Validator for GetAnnouncementsDirectoryQuery.
/// </summary>
public sealed class GetAnnouncementsDirectoryQueryValidator : AbstractValidator<GetAnnouncementsDirectoryQuery>
{
    /// <summary>
    /// Initializes validation rules for GetAnnouncementsDirectoryQuery.
    /// </summary>
    public GetAnnouncementsDirectoryQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetAnnouncementsDirectoryQuery.
/// </summary>
public sealed class GetAnnouncementsDirectoryQueryHandler : IRequestHandler<GetAnnouncementsDirectoryQuery, PagedResult<AnnouncementDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAnnouncementsDirectoryQueryHandler"/>.
    /// </summary>
    public GetAnnouncementsDirectoryQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AnnouncementDto>> Handle(GetAnnouncementsDirectoryQuery request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var adminProfile = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive, cancellationToken);

        var isSuperAdmin = adminProfile != null && (adminProfile.Role == AdminRoleType.SuperAdmin || adminProfile.HasPermission(Permissions.AnnouncementsPublishPlatform));

        var query = _dbContext.Announcements.AsQueryable();

        if (isSuperAdmin)
        {
            if (request.Scope.HasValue)
            {
                query = query.Where(a => a.Scope == request.Scope.Value);
            }
        }
        else
        {
            // Organization HR / Admin must be in active organization context
            var currentOrgId = _orgContext.CurrentOrganizationId;
            if (!currentOrgId.HasValue || currentOrgId.Value == Guid.Empty)
            {
                throw new UnauthorizedAccessException("Active organization context is required.");
            }

            var orgId = currentOrgId.Value;
            var membership = await _dbContext.OrganizationMemberships
                .FirstOrDefaultAsync(m => m.UserId == callerUserId && m.OrganizationId == orgId && m.Status == MembershipStatus.Active, cancellationToken);

            if (membership == null || !membership.HasPermission(Permissions.AnnouncementsPublishWorkplace))
            {
                throw new UnauthorizedAccessException("Insufficient permissions to manage workplace announcements.");
            }

            // Strictly lock to caller's organization
            query = query.Where(a => a.Scope == AnnouncementScope.Workplace && a.OrganizationId == orgId);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(a => a.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(a => a.Title.Contains(search) || a.Description.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.PublishedAtUtc)
            .ThenByDescending(a => a.CreatedAtUtc)
            .ThenByDescending(a => a.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var orgIds = items.Where(a => a.OrganizationId.HasValue).Select(a => a.OrganizationId!.Value).Distinct().ToList();
        var orgs = await _dbContext.Organizations
            .Where(o => orgIds.Contains(o.Id))
            .ToListAsync(cancellationToken);

        var orgMap = orgs.ToDictionary(o => o.Id, o => o.CompanyName);

        var dtos = items.Select(a =>
        {
            string? orgName = null;
            if (a.OrganizationId.HasValue && orgMap.TryGetValue(a.OrganizationId.Value, out var name))
            {
                orgName = name;
            }

            return new AnnouncementDto(
                a.Id,
                a.OrganizationId,
                orgName,
                a.Title,
                a.Description,
                a.Scope,
                a.Status,
                a.PublishedAtUtc,
                a.PublishedByUserId,
                a.CreatedAtUtc,
                a.CreatedByUserId,
                a.UpdatedAtUtc,
                a.UpdatedByUserId,
                a.ArchivedAtUtc,
                a.ArchivedByUserId);
        }).ToList();

        return new PagedResult<AnnouncementDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
