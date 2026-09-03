using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Communication.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Announcements;

/// <summary>
/// Query to retrieve a paginated feed of published workplace announcements for the caller's active organization.
/// </summary>
public sealed record GetWorkplaceAnnouncementsQuery(
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<AnnouncementDto>>;

/// <summary>
/// Validator for GetWorkplaceAnnouncementsQuery.
/// </summary>
public sealed class GetWorkplaceAnnouncementsQueryValidator : AbstractValidator<GetWorkplaceAnnouncementsQuery>
{
    /// <summary>
    /// Initializes validation rules for GetWorkplaceAnnouncementsQuery.
    /// </summary>
    public GetWorkplaceAnnouncementsQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetWorkplaceAnnouncementsQuery.
/// </summary>
public sealed class GetWorkplaceAnnouncementsQueryHandler : IRequestHandler<GetWorkplaceAnnouncementsQuery, PagedResult<AnnouncementDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetWorkplaceAnnouncementsQueryHandler"/>.
    /// </summary>
    public GetWorkplaceAnnouncementsQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AnnouncementDto>> Handle(GetWorkplaceAnnouncementsQuery request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var currentOrgId = _orgContext.CurrentOrganizationId;
        if (!currentOrgId.HasValue || currentOrgId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Active organization context is required to view workplace announcements.");
        }

        var orgId = currentOrgId.Value;

        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(orgId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed: User is not authorized to access organization '{orgId}'.");
        }

        var org = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId, cancellationToken);
        var orgName = org?.CompanyName;

        var query = _dbContext.Announcements
            .Where(a => a.Scope == AnnouncementScope.Workplace && a.OrganizationId == orgId && a.Status == AnnouncementStatus.Published);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.PublishedAtUtc)
            .ThenByDescending(a => a.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(a => new AnnouncementDto(
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
            a.ArchivedByUserId)).ToList();

        return new PagedResult<AnnouncementDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
