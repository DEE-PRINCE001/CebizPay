using System.Text.Json;
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Communication.Enums;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Announcements;

/// <summary>
/// Command to archive an announcement, hiding it from public and workplace feeds.
/// </summary>
public sealed record ArchiveAnnouncementCommand(
    Guid AnnouncementId) : IRequest<bool>;

/// <summary>
/// Validator for ArchiveAnnouncementCommand.
/// </summary>
public sealed class ArchiveAnnouncementCommandValidator : AbstractValidator<ArchiveAnnouncementCommand>
{
    /// <summary>
    /// Initializes validation rules for ArchiveAnnouncementCommand.
    /// </summary>
    public ArchiveAnnouncementCommandValidator()
    {
        RuleFor(x => x.AnnouncementId)
            .NotEmpty().WithMessage("AnnouncementId is required.");
    }
}

/// <summary>
/// Handler for ArchiveAnnouncementCommand.
/// </summary>
public sealed class ArchiveAnnouncementCommandHandler : IRequestHandler<ArchiveAnnouncementCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="ArchiveAnnouncementCommandHandler"/>.
    /// </summary>
    public ArchiveAnnouncementCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<bool> Handle(ArchiveAnnouncementCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var announcement = await _dbContext.Announcements
            .FirstOrDefaultAsync(a => a.Id == request.AnnouncementId, cancellationToken)
            ?? throw new KeyNotFoundException($"Announcement '{request.AnnouncementId}' not found.");

        if (announcement.Scope == AnnouncementScope.Platform)
        {
            var adminProfile = await _dbContext.AdminProfiles
                .FirstOrDefaultAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive, cancellationToken);

            if (adminProfile == null || !adminProfile.HasPermission(Permissions.AnnouncementsPublishPlatform))
            {
                throw new UnauthorizedAccessException("Only active Super Admins can archive platform announcements.");
            }
        }
        else if (announcement.Scope == AnnouncementScope.Workplace)
        {
            var orgId = announcement.OrganizationId!.Value;

            var isSuperAdmin = await _dbContext.AdminProfiles
                .AnyAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive && a.Role == AdminRoleType.SuperAdmin, cancellationToken);

            var membership = await _dbContext.OrganizationMemberships
                .FirstOrDefaultAsync(m => m.UserId == callerUserId && m.OrganizationId == orgId && m.Status == MembershipStatus.Active, cancellationToken);

            if (!isSuperAdmin && (membership == null || !membership.HasPermission(Permissions.AnnouncementsPublishWorkplace)))
            {
                throw new UnauthorizedAccessException("Insufficient permissions to archive workplace announcements for this organization.");
            }
        }

        var now = DateTime.UtcNow;
        announcement.Archive(callerUserId, now);

        var auditLog = AuditLog.Create(
            actorId: callerUserId,
            action: AuditActions.AnnouncementArchived,
            resourceType: AuditResourceTypes.Announcement,
            resourceId: announcement.Id.ToString(),
            organizationId: announcement.OrganizationId,
            afterJson: JsonSerializer.Serialize(new
            {
                announcement.Id,
                announcement.Scope,
                announcement.Title,
                announcement.Status,
                announcement.ArchivedAtUtc
            }));

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
