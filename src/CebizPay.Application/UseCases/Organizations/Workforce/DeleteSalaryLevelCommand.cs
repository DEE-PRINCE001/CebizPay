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
/// Command to delete an organization salary level.
/// </summary>
public sealed record DeleteSalaryLevelCommand(
    Guid SalaryLevelId,
    Guid OrganizationId) : IRequest<bool>;

/// <summary>
/// Validator for DeleteSalaryLevelCommand.
/// </summary>
public sealed class DeleteSalaryLevelCommandValidator : AbstractValidator<DeleteSalaryLevelCommand>
{
    /// <summary>
    /// Initializes validation rules for DeleteSalaryLevelCommand.
    /// </summary>
    public DeleteSalaryLevelCommandValidator()
    {
        RuleFor(x => x.SalaryLevelId).NotEmpty().WithMessage("SalaryLevelId is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
    }
}

/// <summary>
/// Handler for DeleteSalaryLevelCommand.
/// </summary>
public sealed class DeleteSalaryLevelCommandHandler : IRequestHandler<DeleteSalaryLevelCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="DeleteSalaryLevelCommandHandler"/>.
    /// </summary>
    public DeleteSalaryLevelCommandHandler(
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
    public async Task<bool> Handle(DeleteSalaryLevelCommand request, CancellationToken cancellationToken)
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

        var salaryLevel = await _dbContext.SalaryLevels.FirstOrDefaultAsync(
            s => s.Id == request.SalaryLevelId && s.OrganizationId == request.OrganizationId,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Salary level {request.SalaryLevelId} not found in organization {request.OrganizationId}.");

        var assignedStaffCount = await _dbContext.OrganizationMemberships.CountAsync(
            m => m.OrganizationId == request.OrganizationId && m.SalaryLevelId == request.SalaryLevelId && m.Status == MembershipStatus.Active,
            cancellationToken);

        if (assignedStaffCount > 0)
        {
            throw new InvalidOperationException($"Cannot delete salary level '{salaryLevel.LevelName}' because {assignedStaffCount} active staff member(s) are currently assigned to it. Please reassign them first.");
        }

        var beforeJson = System.Text.Json.JsonSerializer.Serialize(new { salaryLevel.Id, salaryLevel.LevelName, salaryLevel.BaseAmount, salaryLevel.Currency });

        _dbContext.SalaryLevels.Remove(salaryLevel);

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.SalaryLevelDeleted,
            resourceType: AuditResourceTypes.SalaryLevel,
            resourceId: salaryLevel.Id.ToString(),
            organizationId: request.OrganizationId,
            beforeJson: beforeJson);
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new SalaryLevelDeletedDomainEvent(salaryLevel.Id, request.OrganizationId, salaryLevel.LevelName, DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
