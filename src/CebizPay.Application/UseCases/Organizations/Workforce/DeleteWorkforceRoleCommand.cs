using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Workforce;

/// <summary>
/// Command to delete an organization workforce role.
/// </summary>
public sealed record DeleteWorkforceRoleCommand(
    Guid RoleId,
    Guid OrganizationId) : IRequest<bool>;

/// <summary>
/// Validator for DeleteWorkforceRoleCommand.
/// </summary>
public sealed class DeleteWorkforceRoleCommandValidator : AbstractValidator<DeleteWorkforceRoleCommand>
{
    /// <summary>
    /// Initializes validation rules for DeleteWorkforceRoleCommand.
    /// </summary>
    public DeleteWorkforceRoleCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("RoleId is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
    }
}

/// <summary>
/// Handler for DeleteWorkforceRoleCommand.
/// </summary>
public sealed class DeleteWorkforceRoleCommandHandler : IRequestHandler<DeleteWorkforceRoleCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="DeleteWorkforceRoleCommandHandler"/>.
    /// </summary>
    public DeleteWorkforceRoleCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService,
        IOutboxService outboxService)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _currentUserService = currentUserService;
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public async Task<bool> Handle(DeleteWorkforceRoleCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization {request.OrganizationId} not found.");

        if (!org.CanConfigureHris())
        {
            throw new InvalidOperationException("Cannot configure HRIS structure while organization status is suspended.");
        }

        var role = await _dbContext.WorkforceRoles.FirstOrDefaultAsync(
            r => r.Id == request.RoleId && r.OrganizationId == request.OrganizationId,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Workforce role {request.RoleId} not found in organization {request.OrganizationId}.");

        var assignedStaffCount = await _dbContext.OrganizationMemberships.CountAsync(
            m => m.OrganizationId == request.OrganizationId && m.WorkforceRoleId == request.RoleId && m.Status == MembershipStatus.Active,
            cancellationToken);

        if (assignedStaffCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete workforce role '{role.Title}' because {assignedStaffCount} active staff member(s) are currently assigned to it. Please reassign them first.");
        }

        var beforeJson = System.Text.Json.JsonSerializer.Serialize(new { role.Id, role.Title, role.DepartmentId, role.Description });

        _dbContext.WorkforceRoles.Remove(role);

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.RoleDeleted,
            resourceType: AuditResourceTypes.WorkforceRole,
            resourceId: role.Id.ToString(),
            organizationId: request.OrganizationId,
            beforeJson: beforeJson);
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new WorkforceRoleDeletedDomainEvent(role.Id, request.OrganizationId, role.Title, DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
