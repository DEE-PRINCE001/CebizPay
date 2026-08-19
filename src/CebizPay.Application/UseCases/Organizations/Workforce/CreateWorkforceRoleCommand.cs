using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Entities;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Workforce;

/// <summary>
/// Command to create a workforce role within an organization.
/// </summary>
public sealed record CreateWorkforceRoleCommand(
    Guid OrganizationId,
    string Title,
    Guid? DepartmentId,
    string? Description) : IRequest<Guid>;

/// <summary>
/// Validator for CreateWorkforceRoleCommand.
/// </summary>
public sealed class CreateWorkforceRoleCommandValidator : AbstractValidator<CreateWorkforceRoleCommand>
{
    /// <summary>
    /// Initializes validation rules for CreateWorkforceRoleCommand.
    /// </summary>
    public CreateWorkforceRoleCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Title).NotEmpty().WithMessage("Role Title is required.").MaximumLength(100);
    }
}

/// <summary>
/// Handler for CreateWorkforceRoleCommand.
/// </summary>
public sealed class CreateWorkforceRoleCommandHandler : IRequestHandler<CreateWorkforceRoleCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="CreateWorkforceRoleCommandHandler"/>.
    /// </summary>
    public CreateWorkforceRoleCommandHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<Guid> Handle(CreateWorkforceRoleCommand request, CancellationToken cancellationToken)
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

        var role = new WorkforceRole(request.OrganizationId, request.Title, request.DepartmentId, request.Description);
        _dbContext.WorkforceRoles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return role.Id;
    }
}
