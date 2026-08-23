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
/// Command to submit a candidate job application for an active job posting.
/// </summary>
public sealed record SubmitApplicationCommand(
    Guid JobPostingId,
    string ApplicantName,
    string ApplicantEmail,
    string ApplicantPhone,
    string? ResumeReference = null,
    string? CoverLetter = null,
    string? ApplicantUserId = null) : IRequest<Guid>;

/// <summary>
/// Validator for SubmitApplicationCommand.
/// </summary>
public sealed class SubmitApplicationCommandValidator : AbstractValidator<SubmitApplicationCommand>
{
    /// <summary>
    /// Initializes validation rules for SubmitApplicationCommand.
    /// </summary>
    public SubmitApplicationCommandValidator()
    {
        RuleFor(x => x.JobPostingId).NotEmpty().WithMessage("JobPostingId is required.");
        RuleFor(x => x.ApplicantName).NotEmpty().WithMessage("Applicant name is required.").MaximumLength(150);
        RuleFor(x => x.ApplicantEmail).NotEmpty().EmailAddress().WithMessage("Valid applicant email is required.").MaximumLength(255);
        RuleFor(x => x.ApplicantPhone).NotEmpty().WithMessage("Applicant phone number is required.").MaximumLength(50);
        RuleFor(x => x.ResumeReference).MaximumLength(1000);
        RuleFor(x => x.CoverLetter).MaximumLength(4000);
    }
}

/// <summary>
/// Handler for SubmitApplicationCommand.
/// </summary>
public sealed class SubmitApplicationCommandHandler : IRequestHandler<SubmitApplicationCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="SubmitApplicationCommandHandler"/>.
    /// </summary>
    public SubmitApplicationCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IOutboxService outboxService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public async Task<Guid> Handle(SubmitApplicationCommand request, CancellationToken cancellationToken)
    {
        var jobPosting = await _dbContext.JobPostings.FirstOrDefaultAsync(
            j => j.Id == request.JobPostingId,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Job posting {request.JobPostingId} not found.");

        var now = DateTime.UtcNow;
        if (!jobPosting.IsAcceptingApplications(now))
        {
            throw new InvalidOperationException("Job posting is not accepting applications or application deadline has passed.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == jobPosting.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization {jobPosting.OrganizationId} not found.");

        if (!org.CanConfigureHris())
        {
            throw new InvalidOperationException("Cannot apply to job postings while organization status is suspended.");
        }

        var applicantUserId = !string.IsNullOrWhiteSpace(request.ApplicantUserId)
            ? request.ApplicantUserId
            : _currentUserService.UserId;

        var normalizedEmail = request.ApplicantEmail.Trim().ToLowerInvariant();

        // Duplicate application protection:
        // Check if active (non-withdrawn, non-rejected) application already exists
#pragma warning disable CA1862, CA1304, CA1311
        var duplicateExists = await _dbContext.RecruitmentApplications.AnyAsync(
            a => a.JobPostingId == request.JobPostingId
                 && (a.ApplicantEmail.ToLower() == normalizedEmail || (applicantUserId != null && a.ApplicantUserId == applicantUserId))
                 && a.Status != ApplicationStatus.Withdrawn
                 && a.Status != ApplicationStatus.Rejected,
            cancellationToken);
#pragma warning restore CA1862, CA1304, CA1311

        if (duplicateExists)
        {
            throw new InvalidOperationException("You have already submitted an active application for this job posting.");
        }

        var application = new RecruitmentApplication(
            jobPosting.Id,
            jobPosting.OrganizationId,
            request.ApplicantName,
            normalizedEmail,
            request.ApplicantPhone,
            applicantUserId,
            request.ResumeReference,
            request.CoverLetter);

        _dbContext.RecruitmentApplications.Add(application);

        var actorUserId = applicantUserId ?? "PUBLIC_CANDIDATE";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.ApplicationSubmitted,
            resourceType: AuditResourceTypes.RecruitmentApplication,
            resourceId: application.Id.ToString(),
            organizationId: jobPosting.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                application.Id,
                application.JobPostingId,
                application.ApplicantName,
                application.ApplicantEmail,
                application.ApplicantPhone,
                application.ApplicantUserId,
                application.Status
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new RecruitmentApplicationSubmittedDomainEvent(
            application.Id,
            jobPosting.Id,
            jobPosting.OrganizationId,
            applicantUserId,
            application.ApplicantName,
            application.ApplicantEmail,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return application.Id;
    }
}
