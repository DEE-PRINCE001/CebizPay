using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Recruitment;

/// <summary>
/// Command to create a draft job posting within an organization.
/// </summary>
public sealed record CreateJobPostingCommand(
    Guid OrganizationId,
    string Title,
    string Description,
    EmploymentType EmploymentType = EmploymentType.FullTime,
    Guid? DepartmentId = null,
    Guid? WorkforceRoleId = null,
    Guid? SalaryLevelId = null,
    string? Location = null,
    string? Requirements = null,
    string? Responsibilities = null,
    DateTime? ApplicationDeadline = null) : IRequest<Guid>;

/// <summary>
/// Validator for CreateJobPostingCommand.
/// </summary>
public sealed class CreateJobPostingCommandValidator : AbstractValidator<CreateJobPostingCommand>
{
    /// <summary>
    /// Initializes validation rules for CreateJobPostingCommand.
    /// </summary>
    public CreateJobPostingCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Title).NotEmpty().WithMessage("Job title is required.").MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().WithMessage("Job description is required.");
        RuleFor(x => x.Location).MaximumLength(150);
        RuleFor(x => x.Requirements).MaximumLength(4000);
        RuleFor(x => x.Responsibilities).MaximumLength(4000);
        RuleFor(x => x.ApplicationDeadline)
            .Must(d => !d.HasValue || d.Value > DateTime.UtcNow)
            .WithMessage("Application deadline must be in the future.");
    }
}

/// <summary>
/// Handler for CreateJobPostingCommand.
/// </summary>
public sealed class CreateJobPostingCommandHandler : IRequestHandler<CreateJobPostingCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="CreateJobPostingCommandHandler"/>.
    /// </summary>
    public CreateJobPostingCommandHandler(
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
    public async Task<Guid> Handle(CreateJobPostingCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot create job postings while organization status is suspended.");
        }

        // Validate workforce references belong to the same organization
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

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";

        var jobPosting = new JobPosting(
            request.OrganizationId,
            request.Title,
            request.Description,
            actorUserId,
            request.EmploymentType,
            request.DepartmentId,
            request.WorkforceRoleId,
            request.SalaryLevelId,
            request.Location,
            request.Requirements,
            request.Responsibilities,
            request.ApplicationDeadline);

        _dbContext.JobPostings.Add(jobPosting);

        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.JobPostingCreated,
            resourceType: AuditResourceTypes.JobPosting,
            resourceId: jobPosting.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                jobPosting.Id,
                jobPosting.Title,
                jobPosting.EmploymentType,
                jobPosting.DepartmentId,
                jobPosting.WorkforceRoleId,
                jobPosting.SalaryLevelId,
                jobPosting.Status
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new JobPostingCreatedDomainEvent(
            jobPosting.Id,
            request.OrganizationId,
            jobPosting.Title,
            actorUserId,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return jobPosting.Id;
    }
}
