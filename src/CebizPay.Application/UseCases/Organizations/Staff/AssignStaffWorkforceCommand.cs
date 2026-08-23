using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Staff;

/// <summary>
/// Command to assign or reassign workforce details (Department, Role, Salary Level) to a staff member.
/// </summary>
public sealed record AssignStaffWorkforceCommand(
    Guid OrganizationId,
    Guid MembershipId,
    Guid? DepartmentId,
    Guid? WorkforceRoleId,
    Guid? SalaryLevelId) : IRequest<bool>;

/// <summary>
/// Validator for AssignStaffWorkforceCommand.
/// </summary>
public sealed class AssignStaffWorkforceCommandValidator : AbstractValidator<AssignStaffWorkforceCommand>
{
    /// <summary>
    /// Initializes validation rules for AssignStaffWorkforceCommand.
    /// </summary>
    public AssignStaffWorkforceCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.MembershipId).NotEmpty().WithMessage("MembershipId is required.");
    }
}

/// <summary>
/// Handler for AssignStaffWorkforceCommand.
/// </summary>
public sealed class AssignStaffWorkforceCommandHandler : IRequestHandler<AssignStaffWorkforceCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="AssignStaffWorkforceCommandHandler"/>.
    /// </summary>
    public AssignStaffWorkforceCommandHandler(
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
    public async Task<bool> Handle(AssignStaffWorkforceCommand request, CancellationToken cancellationToken)
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

        var membership = await _dbContext.OrganizationMemberships.FirstOrDefaultAsync(
            m => m.Id == request.MembershipId && m.OrganizationId == request.OrganizationId,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Staff membership {request.MembershipId} not found in organization {request.OrganizationId}.");

        if (membership.Status == MembershipStatus.Terminated)
        {
            throw new InvalidOperationException("Cannot assign workforce details to a terminated staff member.");
        }

        if (request.DepartmentId.HasValue)
        {
            var deptExists = await _dbContext.Departments.AnyAsync(
                d => d.Id == request.DepartmentId.Value && d.OrganizationId == request.OrganizationId,
                cancellationToken);
            if (!deptExists)
            {
                throw new KeyNotFoundException($"Department {request.DepartmentId.Value} not found in organization {request.OrganizationId}.");
            }
        }

        if (request.WorkforceRoleId.HasValue)
        {
            var roleExists = await _dbContext.WorkforceRoles.AnyAsync(
                r => r.Id == request.WorkforceRoleId.Value && r.OrganizationId == request.OrganizationId,
                cancellationToken);
            if (!roleExists)
            {
                throw new KeyNotFoundException($"Workforce role {request.WorkforceRoleId.Value} not found in organization {request.OrganizationId}.");
            }
        }

        if (request.SalaryLevelId.HasValue)
        {
            var levelExists = await _dbContext.SalaryLevels.AnyAsync(
                s => s.Id == request.SalaryLevelId.Value && s.OrganizationId == request.OrganizationId,
                cancellationToken);
            if (!levelExists)
            {
                throw new KeyNotFoundException($"Salary level {request.SalaryLevelId.Value} not found in organization {request.OrganizationId}.");
            }
        }

        var beforeJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            membership.Id,
            membership.DepartmentId,
            membership.WorkforceRoleId,
            membership.SalaryLevelId
        });

        membership.AssignWorkforceDetails(request.DepartmentId, request.WorkforceRoleId, request.SalaryLevelId);

        var afterJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            membership.Id,
            membership.DepartmentId,
            membership.WorkforceRoleId,
            membership.SalaryLevelId
        });

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.StaffAssigned,
            resourceType: AuditResourceTypes.StaffMember,
            resourceId: membership.Id.ToString(),
            organizationId: request.OrganizationId,
            beforeJson: beforeJson,
            afterJson: afterJson);
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new StaffAssignedDomainEvent(
            membership.Id,
            request.OrganizationId,
            membership.UserId,
            request.DepartmentId,
            request.WorkforceRoleId,
            request.SalaryLevelId,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
