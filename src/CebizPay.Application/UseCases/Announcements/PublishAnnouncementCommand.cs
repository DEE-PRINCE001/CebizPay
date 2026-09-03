using System.Text.Json;
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Communication.Entities;
using CebizPay.Domain.Communication.Enums;
using CebizPay.Domain.Communication.Events;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Announcements;

/// <summary>
/// Command to publish a draft announcement.
/// </summary>
public sealed record PublishAnnouncementCommand(
    Guid AnnouncementId) : IRequest<AnnouncementDto>;

/// <summary>
/// Validator for PublishAnnouncementCommand.
/// </summary>
public sealed class PublishAnnouncementCommandValidator : AbstractValidator<PublishAnnouncementCommand>
{
    /// <summary>
    /// Initializes validation rules for PublishAnnouncementCommand.
    /// </summary>
    public PublishAnnouncementCommandValidator()
    {
        RuleFor(x => x.AnnouncementId)
            .NotEmpty().WithMessage("AnnouncementId is required.");
    }
}

/// <summary>
/// Handler for PublishAnnouncementCommand.
/// </summary>
public sealed class PublishAnnouncementCommandHandler : IRequestHandler<PublishAnnouncementCommand, AnnouncementDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly IOutboxService? _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="PublishAnnouncementCommandHandler"/>.
    /// </summary>
    public PublishAnnouncementCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ICurrentOrganizationContext orgContext,
        IOutboxService? outboxService = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public async Task<AnnouncementDto> Handle(PublishAnnouncementCommand request, CancellationToken cancellationToken)
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

        if (announcement.Scope == AnnouncementScope.Platform)
        {
            var adminProfile = await _dbContext.AdminProfiles
                .FirstOrDefaultAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive, cancellationToken);

            if (adminProfile == null || !adminProfile.HasPermission(Permissions.AnnouncementsPublishPlatform))
            {
                throw new UnauthorizedAccessException("Only active Super Admins can publish platform announcements.");
            }
        }
        else if (announcement.Scope == AnnouncementScope.Workplace)
        {
            var orgId = announcement.OrganizationId!.Value;

            var org = await _dbContext.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId, cancellationToken);

            if (org == null || org.Status == OrganizationStatus.Suspended)
            {
                throw new InvalidOperationException("Cannot publish announcements for a suspended or non-existent organization.");
            }

            orgName = org.CompanyName;

            var isSuperAdmin = await _dbContext.AdminProfiles
                .AnyAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive && a.Role == AdminRoleType.SuperAdmin, cancellationToken);

            var membership = await _dbContext.OrganizationMemberships
                .FirstOrDefaultAsync(m => m.UserId == callerUserId && m.OrganizationId == orgId && m.Status == MembershipStatus.Active, cancellationToken);

            if (!isSuperAdmin && (membership == null || !membership.HasPermission(Permissions.AnnouncementsPublishWorkplace)))
            {
                throw new UnauthorizedAccessException("Insufficient permissions to publish workplace announcements for this organization.");
            }
        }

        var now = DateTime.UtcNow;
        announcement.Publish(callerUserId, now);

        var auditLog = AuditLog.Create(
            actorId: callerUserId,
            action: AuditActions.AnnouncementPublished,
            resourceType: AuditResourceTypes.Announcement,
            resourceId: announcement.Id.ToString(),
            organizationId: announcement.OrganizationId,
            afterJson: JsonSerializer.Serialize(new
            {
                announcement.Id,
                announcement.Scope,
                announcement.Title,
                announcement.Status,
                announcement.PublishedAtUtc
            }));

        _dbContext.AuditLogs.Add(auditLog);

        _outboxService?.Write(new AnnouncementPublishedDomainEvent(
            announcement.Id,
            announcement.Scope,
            announcement.OrganizationId,
            announcement.Title,
            announcement.Description,
            callerUserId,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);

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
