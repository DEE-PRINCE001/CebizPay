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
/// Command to create a new announcement (either global Platform or tenant-isolated Workplace).
/// </summary>
public sealed record CreateAnnouncementCommand(
    AnnouncementScope Scope,
    string Title,
    string Description,
    bool PublishImmediately = false) : IRequest<AnnouncementDto>;

/// <summary>
/// Validator for CreateAnnouncementCommand.
/// </summary>
public sealed class CreateAnnouncementCommandValidator : AbstractValidator<CreateAnnouncementCommand>
{
    /// <summary>
    /// Initializes validation rules for CreateAnnouncementCommand.
    /// </summary>
    public CreateAnnouncementCommandValidator()
    {
        RuleFor(x => x.Scope)
            .IsInEnum().WithMessage("A valid announcement scope (Platform or Workplace) must be specified.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Announcement title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Announcement description is required.")
            .MaximumLength(4000).WithMessage("Description cannot exceed 4000 characters.");
    }
}

/// <summary>
/// Handler for CreateAnnouncementCommand.
/// </summary>
public sealed class CreateAnnouncementCommandHandler : IRequestHandler<CreateAnnouncementCommand, AnnouncementDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly IOutboxService? _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="CreateAnnouncementCommandHandler"/>.
    /// </summary>
    public CreateAnnouncementCommandHandler(
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
    public async Task<AnnouncementDto> Handle(CreateAnnouncementCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        Announcement announcement;
        string? orgName = null;

        if (request.Scope == AnnouncementScope.Platform)
        {
            // Super Admin / Platform Admin authorization
            var adminProfile = await _dbContext.AdminProfiles
                .FirstOrDefaultAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive, cancellationToken);

            if (adminProfile == null || !adminProfile.HasPermission(Permissions.AnnouncementsPublishPlatform))
            {
                throw new UnauthorizedAccessException("Only active Super Admins can publish platform announcements.");
            }

            announcement = Announcement.CreatePlatform(request.Title, request.Description, callerUserId);
        }
        else if (request.Scope == AnnouncementScope.Workplace)
        {
            var currentOrgId = _orgContext.CurrentOrganizationId;
            if (!currentOrgId.HasValue || currentOrgId.Value == Guid.Empty)
            {
                throw new UnauthorizedAccessException("Active organization context is required to publish workplace announcements.");
            }

            var orgId = currentOrgId.Value;

            // Validate organization exists and can perform operations
            var org = await _dbContext.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId, cancellationToken)
                ?? throw new KeyNotFoundException($"Organization '{orgId}' not found.");

            if (org.Status == OrganizationStatus.Suspended)
            {
                throw new InvalidOperationException("Cannot publish announcements while organization is suspended.");
            }

            orgName = org.CompanyName;

            // Validate caller membership and workplace announcement permission in this organization
            var membership = await _dbContext.OrganizationMemberships
                .FirstOrDefaultAsync(m => m.UserId == callerUserId && m.OrganizationId == orgId && m.Status == MembershipStatus.Active, cancellationToken);

            // Super Admin also has access if verified via AdminProfile
            var isSuperAdmin = await _dbContext.AdminProfiles
                .AnyAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive && a.Role == AdminRoleType.SuperAdmin, cancellationToken);

            if (!isSuperAdmin && (membership == null || !membership.HasPermission(Permissions.AnnouncementsPublishWorkplace)))
            {
                throw new UnauthorizedAccessException("Insufficient permissions to publish workplace announcements for this organization.");
            }

            announcement = Announcement.CreateWorkplace(orgId, request.Title, request.Description, callerUserId);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Invalid announcement scope.");
        }

        var now = DateTime.UtcNow;
        if (request.PublishImmediately)
        {
            announcement.Publish(callerUserId, now);
        }

        _dbContext.Announcements.Add(announcement);

        // Record audit entry
        var action = request.PublishImmediately ? AuditActions.AnnouncementPublished : AuditActions.AnnouncementCreated;
        var auditLog = AuditLog.Create(
            actorId: callerUserId,
            action: action,
            resourceType: AuditResourceTypes.Announcement,
            resourceId: announcement.Id.ToString(),
            organizationId: announcement.OrganizationId,
            afterJson: JsonSerializer.Serialize(new
            {
                announcement.Id,
                announcement.Scope,
                announcement.Title,
                announcement.Status,
                announcement.OrganizationId,
                announcement.PublishedAtUtc
            }));

        _dbContext.AuditLogs.Add(auditLog);

        if (request.PublishImmediately)
        {
            _outboxService?.Write(new AnnouncementPublishedDomainEvent(
                announcement.Id,
                announcement.Scope,
                announcement.OrganizationId,
                announcement.Title,
                announcement.Description,
                callerUserId,
                now));
        }

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
