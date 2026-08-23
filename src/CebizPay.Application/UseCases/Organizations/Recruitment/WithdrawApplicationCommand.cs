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
/// Command for a candidate to withdraw their active job application.
/// </summary>
public sealed record WithdrawApplicationCommand(Guid ApplicationId) : IRequest<bool>;

/// <summary>
/// Validator for WithdrawApplicationCommand.
/// </summary>
public sealed class WithdrawApplicationCommandValidator : AbstractValidator<WithdrawApplicationCommand>
{
    /// <summary>
    /// Initializes validation rules for WithdrawApplicationCommand.
    /// </summary>
    public WithdrawApplicationCommandValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty().WithMessage("ApplicationId is required.");
    }
}

/// <summary>
/// Handler for WithdrawApplicationCommand.
/// </summary>
public sealed class WithdrawApplicationCommandHandler : IRequestHandler<WithdrawApplicationCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="WithdrawApplicationCommandHandler"/>.
    /// </summary>
    public WithdrawApplicationCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IOutboxService outboxService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public async Task<bool> Handle(WithdrawApplicationCommand request, CancellationToken cancellationToken)
    {
        var application = await _dbContext.RecruitmentApplications.FirstOrDefaultAsync(
            a => a.Id == request.ApplicationId,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Application {request.ApplicationId} not found.");

        var currentUserId = _currentUserService.UserId;
        if (!string.IsNullOrWhiteSpace(application.ApplicantUserId) && application.ApplicantUserId != currentUserId)
        {
            throw new UnauthorizedAccessException("You are not authorized to withdraw this application.");
        }

        var now = DateTime.UtcNow;
        application.Withdraw(now);

        var actorUserId = currentUserId ?? application.ApplicantEmail;
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.ApplicationWithdrawn,
            resourceType: AuditResourceTypes.RecruitmentApplication,
            resourceId: application.Id.ToString(),
            organizationId: application.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                application.Id,
                application.JobPostingId,
                application.Status
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new RecruitmentApplicationWithdrawnDomainEvent(
            application.Id,
            application.JobPostingId,
            application.OrganizationId,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
