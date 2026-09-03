using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Communication.Enums;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Announcements;

/// <summary>
/// Query to retrieve a single announcement by ID with strict tenant isolation and visibility checks.
/// </summary>
public sealed record GetAnnouncementByIdQuery(
    Guid AnnouncementId) : IRequest<AnnouncementDto>;

/// <summary>
/// Validator for GetAnnouncementByIdQuery.
/// </summary>
public sealed class GetAnnouncementByIdQueryValidator : AbstractValidator<GetAnnouncementByIdQuery>
{
    /// <summary>
    /// Initializes validation rules for GetAnnouncementByIdQuery.
    /// </summary>
    public GetAnnouncementByIdQueryValidator()
    {
        RuleFor(x => x.AnnouncementId)
            .NotEmpty().WithMessage("AnnouncementId is required.");
    }
}

/// <summary>
/// Handler for GetAnnouncementByIdQuery.
/// </summary>
public sealed class GetAnnouncementByIdQueryHandler : IRequestHandler<GetAnnouncementByIdQuery, AnnouncementDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAnnouncementByIdQueryHandler"/>.
    /// </summary>
    public GetAnnouncementByIdQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    /// <inheritdoc/>
    public async Task<AnnouncementDto> Handle(GetAnnouncementByIdQuery request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var announcement = await _dbContext.Announcements
            .FirstOrDefaultAsync(a => a.Id == request.AnnouncementId, cancellationToken)
            ?? throw new KeyNotFoundException($"Announcement '{request.AnnouncementId}' not found.");

        string? orgName = null;

        var isSuperAdmin = await _dbContext.AdminProfiles
            .AnyAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive && a.Role == AdminRoleType.SuperAdmin, cancellationToken);

        if (announcement.Scope == AnnouncementScope.Platform)
        {
            if (announcement.Status != AnnouncementStatus.Published && !isSuperAdmin && announcement.CreatedByUserId != callerUserId)
            {
                throw new KeyNotFoundException($"Announcement '{request.AnnouncementId}' not found.");
            }
        }
        else if (announcement.Scope == AnnouncementScope.Workplace)
        {
            var orgId = announcement.OrganizationId!.Value;

            // Strict tenant isolation: Caller must belong to the same organization as the announcement.
            // If the caller is not in this organization, return 404 (do not leak existence).
            var currentOrgId = _orgContext.CurrentOrganizationId;
            if (!isSuperAdmin && (!currentOrgId.HasValue || currentOrgId.Value != orgId))
            {
                throw new KeyNotFoundException($"Announcement '{request.AnnouncementId}' not found.");
            }

            var hasAccess = await _orgContext.HasAccessToOrganizationAsync(orgId, cancellationToken);
            if (!hasAccess && !isSuperAdmin)
            {
                throw new KeyNotFoundException($"Announcement '{request.AnnouncementId}' not found.");
            }

            var membership = await _dbContext.OrganizationMemberships
                .FirstOrDefaultAsync(m => m.UserId == callerUserId && m.OrganizationId == orgId && m.Status == MembershipStatus.Active, cancellationToken);

            var canManage = isSuperAdmin || (membership != null && membership.HasPermission(Permissions.AnnouncementsPublishWorkplace));

            if (announcement.Status != AnnouncementStatus.Published && !canManage && announcement.CreatedByUserId != callerUserId)
            {
                throw new KeyNotFoundException($"Announcement '{request.AnnouncementId}' not found.");
            }

            var org = await _dbContext.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId, cancellationToken);
            orgName = org?.CompanyName;
        }

        return new AnnouncementDto(
            announcement.Id,
            announcement.OrganizationId,
            orgName,
            announcement.Title,
            announcement.Description,
            announcement.Scope,
            announcement.Status,
            announcement.PublishedAtUtc,
            announcement.PublishedByUserId,
            announcement.CreatedAtUtc,
            announcement.CreatedByUserId,
            announcement.UpdatedAtUtc,
            announcement.UpdatedByUserId,
            announcement.ArchivedAtUtc,
            announcement.ArchivedByUserId);
    }
}
