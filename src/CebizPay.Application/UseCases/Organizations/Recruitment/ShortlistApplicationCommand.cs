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
/// Command to shortlist a candidate application.
/// </summary>
public sealed record ShortlistApplicationCommand(
    Guid ApplicationId,
    Guid OrganizationId,
    string? Notes = null) : IRequest<bool>;

/// <summary>
/// Validator for ShortlistApplicationCommand.
/// </summary>
public sealed class ShortlistApplicationCommandValidator : AbstractValidator<ShortlistApplicationCommand>
{
    /// <summary>
    /// Initializes validation rules for ShortlistApplicationCommand.
    /// </summary>
    public ShortlistApplicationCommandValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty().WithMessage("ApplicationId is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

/// <summary>
/// Handler for ShortlistApplicationCommand.
/// </summary>
public sealed class ShortlistApplicationCommandHandler : IRequestHandler<ShortlistApplicationCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="ShortlistApplicationCommandHandler"/>.
    /// </summary>
    public ShortlistApplicationCommandHandler(
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
    public async Task<bool> Handle(ShortlistApplicationCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Cannot shortlist applications while organization status is suspended.");
        }

        var application = await _dbContext.RecruitmentApplications.FirstOrDefaultAsync(
            a => a.Id == request.ApplicationId && a.OrganizationId == request.OrganizationId,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Application {request.ApplicationId} not found in organization {request.OrganizationId}.");

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var now = DateTime.UtcNow;

        application.Shortlist(actorUserId, now, request.Notes);

        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.ApplicationShortlisted,
            resourceType: AuditResourceTypes.RecruitmentApplication,
            resourceId: application.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                application.Id,
                application.JobPostingId,
                application.Status,
                application.ReviewedByUserId,
                application.ReviewedAtUtc,
                application.ReviewNotes
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new RecruitmentApplicationShortlistedDomainEvent(
            application.Id,
            application.JobPostingId,
            request.OrganizationId,
            actorUserId,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
