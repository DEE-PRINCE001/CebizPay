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
/// Command to publish a draft job posting to start accepting candidate applications.
/// </summary>
public sealed record PublishJobPostingCommand(
    Guid JobPostingId,
    Guid OrganizationId) : IRequest<bool>;

/// <summary>
/// Validator for PublishJobPostingCommand.
/// </summary>
public sealed class PublishJobPostingCommandValidator : AbstractValidator<PublishJobPostingCommand>
{
    /// <summary>
    /// Initializes validation rules for PublishJobPostingCommand.
    /// </summary>
    public PublishJobPostingCommandValidator()
    {
        RuleFor(x => x.JobPostingId).NotEmpty().WithMessage("JobPostingId is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
    }
}

/// <summary>
/// Handler for PublishJobPostingCommand.
/// </summary>
public sealed class PublishJobPostingCommandHandler : IRequestHandler<PublishJobPostingCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="PublishJobPostingCommandHandler"/>.
    /// </summary>
    public PublishJobPostingCommandHandler(
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
    public async Task<bool> Handle(PublishJobPostingCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot publish job postings while organization status is suspended.");
        }

        var jobPosting = await _dbContext.JobPostings.FirstOrDefaultAsync(
            j => j.Id == request.JobPostingId && j.OrganizationId == request.OrganizationId,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Job posting {request.JobPostingId} not found in organization {request.OrganizationId}.");

        var now = DateTime.UtcNow;
        jobPosting.Publish(now);

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.JobPostingPublished,
            resourceType: AuditResourceTypes.JobPosting,
            resourceId: jobPosting.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                jobPosting.Id,
                jobPosting.Title,
                jobPosting.Status,
                jobPosting.PublishedAtUtc
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new JobPostingPublishedDomainEvent(
            jobPosting.Id,
            request.OrganizationId,
            jobPosting.Title,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
