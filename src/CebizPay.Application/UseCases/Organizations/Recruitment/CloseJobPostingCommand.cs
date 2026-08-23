using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Events;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Recruitment;

/// <summary>
/// Command to close an active job posting, terminating candidate application intake.
/// </summary>
public sealed record CloseJobPostingCommand(
    Guid JobPostingId,
    Guid OrganizationId) : IRequest<bool>;

/// <summary>
/// Validator for CloseJobPostingCommand.
/// </summary>
public sealed class CloseJobPostingCommandValidator : AbstractValidator<CloseJobPostingCommand>
{
    /// <summary>
    /// Initializes validation rules for CloseJobPostingCommand.
    /// </summary>
    public CloseJobPostingCommandValidator()
    {
        RuleFor(x => x.JobPostingId).NotEmpty().WithMessage("JobPostingId is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
    }
}

/// <summary>
/// Handler for CloseJobPostingCommand.
/// </summary>
public sealed class CloseJobPostingCommandHandler : IRequestHandler<CloseJobPostingCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="CloseJobPostingCommandHandler"/>.
    /// </summary>
    public CloseJobPostingCommandHandler(
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
    public async Task<bool> Handle(CloseJobPostingCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot close job postings while organization status is suspended.");
        }

        var jobPosting = await _dbContext.JobPostings.FirstOrDefaultAsync(
            j => j.Id == request.JobPostingId && j.OrganizationId == request.OrganizationId,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Job posting {request.JobPostingId} not found in organization {request.OrganizationId}.");

        var now = DateTime.UtcNow;
        jobPosting.Close(now);

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.JobPostingClosed,
            resourceType: AuditResourceTypes.JobPosting,
            resourceId: jobPosting.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                jobPosting.Id,
                jobPosting.Title,
                jobPosting.Status,
                jobPosting.ClosedAtUtc
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new JobPostingClosedDomainEvent(
            jobPosting.Id,
            request.OrganizationId,
            jobPosting.Title,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
