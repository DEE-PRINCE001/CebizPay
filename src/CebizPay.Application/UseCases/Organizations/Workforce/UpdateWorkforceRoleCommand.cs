using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Events;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Workforce;

/// <summary>
/// Command to update an organization workforce role.
/// </summary>
public sealed record UpdateWorkforceRoleCommand(
    Guid RoleId,
    Guid OrganizationId,
    string Title,
    Guid? DepartmentId,
    string? Description) : IRequest<Guid>;

/// <summary>
/// Validator for UpdateWorkforceRoleCommand.
/// </summary>
public sealed class UpdateWorkforceRoleCommandValidator : AbstractValidator<UpdateWorkforceRoleCommand>
{
    /// <summary>
    /// Initializes validation rules for UpdateWorkforceRoleCommand.
    /// </summary>
    public UpdateWorkforceRoleCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("RoleId is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Title).NotEmpty().WithMessage("Role Title is required.").MaximumLength(100);
    }
}

/// <summary>
/// Handler for UpdateWorkforceRoleCommand.
/// </summary>
public sealed class UpdateWorkforceRoleCommandHandler : IRequestHandler<UpdateWorkforceRoleCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="UpdateWorkforceRoleCommandHandler"/>.
    /// </summary>
    public UpdateWorkforceRoleCommandHandler(
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
    public async Task<Guid> Handle(UpdateWorkforceRoleCommand request, CancellationToken cancellationToken)
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

        var trimmedTitle = request.Title.Trim();
        var lowerTitle = trimmedTitle.ToLowerInvariant();

#pragma warning disable CA1862, CA1304, CA1311
        var duplicateExists = await _dbContext.WorkforceRoles.AnyAsync(
            r => r.OrganizationId == request.OrganizationId && r.Id != request.RoleId && r.Title.ToLower() == lowerTitle,
            cancellationToken);
#pragma warning restore CA1862, CA1304, CA1311

        if (duplicateExists)
        {
            throw new InvalidOperationException($"Another workforce role with title '{trimmedTitle}' already exists in this organization.");
        }

        var beforeJson = System.Text.Json.JsonSerializer.Serialize(new { role.Id, role.Title, role.DepartmentId, role.Description });

        role.Update(trimmedTitle, request.DepartmentId, request.Description);

        var afterJson = System.Text.Json.JsonSerializer.Serialize(new { role.Id, role.Title, role.DepartmentId, role.Description });

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.RoleUpdated,
            resourceType: AuditResourceTypes.WorkforceRole,
            resourceId: role.Id.ToString(),
            organizationId: request.OrganizationId,
            beforeJson: beforeJson,
            afterJson: afterJson);
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new WorkforceRoleUpdatedDomainEvent(role.Id, request.OrganizationId, role.Title, role.DepartmentId, DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return role.Id;
    }
}
