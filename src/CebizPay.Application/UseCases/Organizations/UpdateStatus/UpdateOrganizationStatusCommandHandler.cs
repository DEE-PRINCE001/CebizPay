using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Events;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.UpdateStatus;

/// <summary>
/// Handler for UpdateOrganizationStatusCommand.
/// </summary>
public sealed class UpdateOrganizationStatusCommandHandler : IRequestHandler<UpdateOrganizationStatusCommand, UpdateOrganizationStatusResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher;
    private readonly CebizPay.Application.Common.Interfaces.Security.ICurrentUserService? _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="UpdateOrganizationStatusCommandHandler"/>.
    /// </summary>
    public UpdateOrganizationStatusCommandHandler(
        IApplicationDbContext dbContext,
        IEventPublisher eventPublisher,
        CebizPay.Application.Common.Interfaces.Security.ICurrentUserService? currentUserService = null)
    {
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
        _currentUserService = currentUserService;
    }

    /// <inheritdoc/>
    public async Task<UpdateOrganizationStatusResponseDto> Handle(UpdateOrganizationStatusCommand request, CancellationToken cancellationToken)
    {
        var actorId = _currentUserService?.UserId ?? request.AdminUserId;
        if (!string.IsNullOrWhiteSpace(actorId) && actorId != "SYSTEM")
        {
            var admin = await _dbContext.AdminProfiles
                .FirstOrDefaultAsync(a => a.UserId == actorId && !a.IsDeleted && a.IsActive, cancellationToken);

            var requiredPermission = request.NewStatus == Domain.Enums.OrganizationStatus.Suspended
                ? Domain.Permissions.Permissions.OrganizationsSuspend
                : Domain.Permissions.Permissions.OrganizationsReactivate;

            if (admin == null || (admin.Role != Domain.Enums.AdminRoleType.SuperAdmin && !admin.HasPermission(requiredPermission)))
            {
                throw new UnauthorizedAccessException("Caller is not authorized to update organization status.");
            }
        }

        var org = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization with ID {request.OrganizationId} was not found.");

        var oldStatus = org.Status;
        org.TransitionStatus(request.NewStatus, request.Reason);

        var action = request.NewStatus == Domain.Enums.OrganizationStatus.Suspended
            ? Domain.Auditing.AuditActions.OrganizationSuspended
            : Domain.Auditing.AuditActions.OrganizationReactivated;

        var auditActorId = actorId ?? "SYSTEM";

        _dbContext.AuditLogs.Add(Domain.Entities.AuditLog.Create(
            actorId: auditActorId,
            action: action,
            resourceType: Domain.Auditing.AuditResourceTypes.Organization,
            resourceId: org.Id.ToString(),
            organizationId: org.Id,
            afterJson: request.Reason != null ? System.Text.Json.JsonSerializer.Serialize(new { Reason = request.Reason, OldStatus = oldStatus.ToString(), NewStatus = request.NewStatus.ToString() }) : null));

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(
            new OrganizationStatusChangedDomainEvent(
                org.Id, oldStatus, org.Status, request.Reason, DateTime.UtcNow),
            cancellationToken);

        return new UpdateOrganizationStatusResponseDto(org.Id, org.Status.ToString(), org.KybStatus.ToString());
    }
}
