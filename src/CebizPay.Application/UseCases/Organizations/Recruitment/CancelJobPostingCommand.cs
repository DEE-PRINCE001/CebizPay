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
/// Command to cancel a draft or published job posting.
/// </summary>
public sealed record CancelJobPostingCommand(
    Guid JobPostingId,
    Guid OrganizationId) : IRequest<bool>;

/// <summary>
/// Validator for CancelJobPostingCommand.
/// </summary>
public sealed class CancelJobPostingCommandValidator : AbstractValidator<CancelJobPostingCommand>
{
    /// <summary>
    /// Initializes validation rules for CancelJobPostingCommand.
    /// </summary>
    public CancelJobPostingCommandValidator()
    {
        RuleFor(x => x.JobPostingId).NotEmpty().WithMessage("JobPostingId is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
    }
}

/// <summary>
/// Handler for CancelJobPostingCommand.
/// </summary>
public sealed class CancelJobPostingCommandHandler : IRequestHandler<CancelJobPostingCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="CancelJobPostingCommandHandler"/>.
    /// </summary>
    public CancelJobPostingCommandHandler(
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
    public async Task<bool> Handle(CancelJobPostingCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot cancel job postings while organization status is suspended.");
        }

        var jobPosting = await _dbContext.JobPostings.FirstOrDefaultAsync(
            j => j.Id == request.JobPostingId && j.OrganizationId == request.OrganizationId,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Job posting {request.JobPostingId} not found in organization {request.OrganizationId}.");

        var now = DateTime.UtcNow;
        jobPosting.Cancel(now);

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.JobPostingCancelled,
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

        _outboxService.Write(new JobPostingCancelledDomainEvent(
            jobPosting.Id,
            request.OrganizationId,
            jobPosting.Title,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
