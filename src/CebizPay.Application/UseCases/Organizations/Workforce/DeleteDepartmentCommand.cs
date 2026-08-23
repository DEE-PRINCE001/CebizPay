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
/// Command to delete an organization department.
/// </summary>
public sealed record DeleteDepartmentCommand(
    Guid DepartmentId,
    Guid OrganizationId) : IRequest<bool>;

/// <summary>
/// Validator for DeleteDepartmentCommand.
/// </summary>
public sealed class DeleteDepartmentCommandValidator : AbstractValidator<DeleteDepartmentCommand>
{
    /// <summary>
    /// Initializes validation rules for DeleteDepartmentCommand.
    /// </summary>
    public DeleteDepartmentCommandValidator()
    {
        RuleFor(x => x.DepartmentId).NotEmpty().WithMessage("DepartmentId is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
    }
}

/// <summary>
/// Handler for DeleteDepartmentCommand.
/// </summary>
public sealed class DeleteDepartmentCommandHandler : IRequestHandler<DeleteDepartmentCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="DeleteDepartmentCommandHandler"/>.
    /// </summary>
    public DeleteDepartmentCommandHandler(
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
    public async Task<bool> Handle(DeleteDepartmentCommand request, CancellationToken cancellationToken)
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

        var dept = await _dbContext.Departments.FirstOrDefaultAsync(
            d => d.Id == request.DepartmentId && d.OrganizationId == request.OrganizationId,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Department {request.DepartmentId} not found in organization {request.OrganizationId}.");

        var assignedStaffCount = await _dbContext.OrganizationMemberships.CountAsync(
            m => m.OrganizationId == request.OrganizationId && m.DepartmentId == request.DepartmentId && m.Status == MembershipStatus.Active,
            cancellationToken);

        if (assignedStaffCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete department '{dept.Name}' because {assignedStaffCount} active staff member(s) are currently assigned to it. Please reassign them first.");
        }

        var beforeJson = System.Text.Json.JsonSerializer.Serialize(new { dept.Id, dept.Name, dept.Description });

        _dbContext.Departments.Remove(dept);

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.DepartmentDeleted,
            resourceType: AuditResourceTypes.Department,
            resourceId: dept.Id.ToString(),
            organizationId: request.OrganizationId,
            beforeJson: beforeJson);
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new DepartmentDeletedDomainEvent(dept.Id, request.OrganizationId, dept.Name, DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
