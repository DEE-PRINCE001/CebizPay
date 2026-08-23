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
/// Command to update an organization department.
/// </summary>
public sealed record UpdateDepartmentCommand(
    Guid DepartmentId,
    Guid OrganizationId,
    string Name,
    string? Description) : IRequest<Guid>;

/// <summary>
/// Validator for UpdateDepartmentCommand.
/// </summary>
public sealed class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    /// <summary>
    /// Initializes validation rules for UpdateDepartmentCommand.
    /// </summary>
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(x => x.DepartmentId).NotEmpty().WithMessage("DepartmentId is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Department Name is required.").MaximumLength(100);
    }
}

/// <summary>
/// Handler for UpdateDepartmentCommand.
/// </summary>
public sealed class UpdateDepartmentCommandHandler : IRequestHandler<UpdateDepartmentCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="UpdateDepartmentCommandHandler"/>.
    /// </summary>
    public UpdateDepartmentCommandHandler(
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
    public async Task<Guid> Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
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

        var trimmedName = request.Name.Trim();
        var lowerName = trimmedName.ToLowerInvariant();

#pragma warning disable CA1862, CA1304, CA1311
        var duplicateExists = await _dbContext.Departments.AnyAsync(
            d => d.OrganizationId == request.OrganizationId && d.Id != request.DepartmentId && d.Name.ToLower() == lowerName,
            cancellationToken);
#pragma warning restore CA1862, CA1304, CA1311

        if (duplicateExists)
        {
            throw new InvalidOperationException($"Another department with name '{trimmedName}' already exists in this organization.");
        }

        var beforeJson = System.Text.Json.JsonSerializer.Serialize(new { dept.Id, dept.Name, dept.Description });

        dept.Update(trimmedName, request.Description);

        var afterJson = System.Text.Json.JsonSerializer.Serialize(new { dept.Id, dept.Name, dept.Description });

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.DepartmentUpdated,
            resourceType: AuditResourceTypes.Department,
            resourceId: dept.Id.ToString(),
            organizationId: request.OrganizationId,
            beforeJson: beforeJson,
            afterJson: afterJson);
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new DepartmentUpdatedDomainEvent(dept.Id, request.OrganizationId, dept.Name, DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return dept.Id;
    }
}
