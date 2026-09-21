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
/// Summary DTO for a workforce role created as part of a department.
/// </summary>
public sealed record RoleSummaryDto(
    Guid Id,
    string Title);

/// <summary>
/// Response DTO returning a department and its atomically created workforce roles.
/// </summary>
public sealed record DepartmentWithRolesDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    IReadOnlyList<RoleSummaryDto> Roles,
    DateTime CreatedAtUtc);

/// <summary>
/// Command to atomically create an organization department and its initial workforce roles.
/// </summary>
public sealed record CreateDepartmentWithRolesCommand(
    Guid OrganizationId,
    string Name,
    string? Description,
    IReadOnlyList<string>? Roles = null) : IRequest<DepartmentWithRolesDto>;

/// <summary>
/// Validator for CreateDepartmentWithRolesCommand.
/// </summary>
public sealed class CreateDepartmentWithRolesCommandValidator : AbstractValidator<CreateDepartmentWithRolesCommand>
{
    /// <summary>
    /// Initializes validation rules for CreateDepartmentWithRolesCommand.
    /// </summary>
    public CreateDepartmentWithRolesCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty()
            .WithMessage("OrganizationId is required.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Department Name is required.")
            .MaximumLength(100);

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Description));

        When(x => x.Roles != null && x.Roles.Count > 0, () =>
        {
            RuleForEach(x => x.Roles)
                .NotEmpty()
                .WithMessage("Role title cannot be empty.")
                .MaximumLength(100)
                .WithMessage("Role title cannot exceed 100 characters.");

            RuleFor(x => x.Roles)
                .Must(roles => roles == null || roles.Select(r => r.Trim().ToLowerInvariant()).Distinct().Count() == roles.Count)
                .WithMessage("Role titles must be distinct.");
        });
    }
}

/// <summary>
/// Handler for CreateDepartmentWithRolesCommand.
/// </summary>
public sealed class CreateDepartmentWithRolesCommandHandler : IRequestHandler<CreateDepartmentWithRolesCommand, DepartmentWithRolesDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="CreateDepartmentWithRolesCommandHandler"/>.
    /// </summary>
    public CreateDepartmentWithRolesCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService,
        IOutboxService outboxService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
    }

    /// <inheritdoc/>
    public async Task<DepartmentWithRolesDto> Handle(CreateDepartmentWithRolesCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Organization {request.OrganizationId} not found.");

        if (!org.CanConfigureHris())
        {
            throw new InvalidOperationException("Cannot configure HRIS structure while organization status is suspended.");
        }

        var trimmedName = request.Name.Trim();
        var lowerName = trimmedName.ToLowerInvariant();

#pragma warning disable CA1862, CA1304, CA1311
        var exists = await _dbContext.Departments.AnyAsync(
            d => d.OrganizationId == request.OrganizationId && d.Name.ToLower() == lowerName,
            cancellationToken).ConfigureAwait(false);
#pragma warning restore CA1862, CA1304, CA1311

        if (exists)
        {
            throw new InvalidOperationException($"Department with name '{trimmedName}' already exists in this organization.");
        }

        var department = new Department(request.OrganizationId, trimmedName, request.Description);
        _dbContext.Departments.Add(department);

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var now = DateTime.UtcNow;

        var deptAuditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.DepartmentCreated,
            resourceType: AuditResourceTypes.Department,
            resourceId: department.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new { department.Id, department.Name, department.Description }));
        _dbContext.AuditLogs.Add(deptAuditLog);

        _outboxService.Write(new DepartmentCreatedDomainEvent(
            DepartmentId: department.Id,
            OrganizationId: request.OrganizationId,
            Name: department.Name,
            OccurredOnUtc: now));

        var roleDtos = new List<RoleSummaryDto>();

        if (request.Roles != null && request.Roles.Count > 0)
        {
            foreach (var roleTitle in request.Roles)
            {
                var trimmedRoleTitle = roleTitle.Trim();
                if (string.IsNullOrWhiteSpace(trimmedRoleTitle))
                {
                    continue;
                }

                var role = new WorkforceRole(request.OrganizationId, trimmedRoleTitle, department.Id, null);
                _dbContext.WorkforceRoles.Add(role);

                var roleAuditLog = AuditLog.Create(
                    actorId: actorUserId,
                    action: AuditActions.RoleCreated,
                    resourceType: AuditResourceTypes.WorkforceRole,
                    resourceId: role.Id.ToString(),
                    organizationId: request.OrganizationId,
                    afterJson: System.Text.Json.JsonSerializer.Serialize(new { role.Id, role.Title, DepartmentId = department.Id }));
                _dbContext.AuditLogs.Add(roleAuditLog);

                _outboxService.Write(new WorkforceRoleCreatedDomainEvent(
                    RoleId: role.Id,
                    OrganizationId: request.OrganizationId,
                    Title: role.Title,
                    DepartmentId: department.Id,
                    OccurredOnUtc: now));

                roleDtos.Add(new RoleSummaryDto(role.Id, role.Title));
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new DepartmentWithRolesDto(
            Id: department.Id,
            OrganizationId: department.OrganizationId,
            Name: department.Name,
            Description: department.Description,
            Roles: roleDtos,
            CreatedAtUtc: department.CreatedAtUtc);
    }
}
