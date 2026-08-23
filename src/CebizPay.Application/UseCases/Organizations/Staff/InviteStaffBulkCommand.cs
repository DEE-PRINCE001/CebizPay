using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Staff;

/// <summary>
/// Command to send staff invitations in bulk to a bounded list of email addresses.
/// </summary>
public sealed record InviteStaffBulkCommand(
    Guid OrganizationId,
    List<string> Emails) : IRequest<BulkInviteSummaryDto>;

/// <summary>
/// Validator for InviteStaffBulkCommand.
/// </summary>
public sealed class InviteStaffBulkCommandValidator : AbstractValidator<InviteStaffBulkCommand>
{
    private const int MaxBatchSize = 50;

    /// <summary>
    /// Initializes validation rules for InviteStaffBulkCommand.
    /// </summary>
    public InviteStaffBulkCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Emails).NotEmpty().WithMessage("Emails list cannot be empty.")
            .Must(e => e != null && e.Count <= MaxBatchSize).WithMessage($"Cannot invite more than {MaxBatchSize} emails at once.");
        RuleForEach(x => x.Emails).NotEmpty().EmailAddress().WithMessage("Each email must be a valid email address.");
    }
}

/// <summary>
/// Handler for InviteStaffBulkCommand.
/// </summary>
public sealed class InviteStaffBulkCommandHandler : IRequestHandler<InviteStaffBulkCommand, BulkInviteSummaryDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="InviteStaffBulkCommandHandler"/>.
    /// </summary>
    public InviteStaffBulkCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService,
        IIdentityService identityService,
        IOutboxService outboxService)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _currentUserService = currentUserService;
        _identityService = identityService;
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public async Task<BulkInviteSummaryDto> Handle(InviteStaffBulkCommand request, CancellationToken cancellationToken)
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

        var deduplicatedEmails = request.Emails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        var results = new List<BulkInviteItemResultDto>();
        int successCount = 0;
        int failedCount = 0;

        foreach (var email in deduplicatedEmails)
        {
            // Check if already an active member in this organization
            var (foundUser, userId, _, _) = await _identityService.FindUserByEmailAsync(email, cancellationToken);
            if (foundUser)
            {
                var isMember = await _dbContext.OrganizationMemberships.AnyAsync(
                    m => m.UserId == userId && m.OrganizationId == request.OrganizationId && m.Status == MembershipStatus.Active,
                    cancellationToken);

                if (isMember)
                {
                    results.Add(new BulkInviteItemResultDto(email, false, null, null, "User is already an active member of this organization."));
                    failedCount++;
                    continue;
                }
            }

            // Check if active pending invitation exists
#pragma warning disable CA1862, CA1304, CA1311
            var existingInvitation = await _dbContext.StaffInvitations.FirstOrDefaultAsync(
                i => i.OrganizationId == request.OrganizationId && i.Email.ToLower() == email && i.Status == InvitationStatus.Pending && i.ExpiresAtUtc > DateTime.UtcNow,
                cancellationToken);
#pragma warning restore CA1862, CA1304, CA1311

            if (existingInvitation != null)
            {
                results.Add(new BulkInviteItemResultDto(
                    email,
                    true,
                    existingInvitation.Id,
                    existingInvitation.InvitationCode,
                    "Active invitation already exists."));
                successCount++;
                continue;
            }

            var invitation = new StaffInvitation(request.OrganizationId, email);
            _dbContext.StaffInvitations.Add(invitation);

            _outboxService.Write(new StaffInvitationCreatedDomainEvent(
                invitation.Id,
                invitation.OrganizationId,
                invitation.Email,
                invitation.InvitationCode,
                invitation.ExpiresAtUtc,
                DateTime.UtcNow));

            results.Add(new BulkInviteItemResultDto(
                email,
                true,
                invitation.Id,
                invitation.InvitationCode,
                null));
            successCount++;
        }

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.StaffBulkInvited,
            resourceType: AuditResourceTypes.StaffMember,
            resourceId: request.OrganizationId.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                TotalRequested = deduplicatedEmails.Count,
                SuccessCount = successCount,
                FailedCount = failedCount
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new StaffBulkInvitationsCreatedDomainEvent(
            request.OrganizationId,
            successCount,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new BulkInviteSummaryDto(
            deduplicatedEmails.Count,
            successCount,
            failedCount,
            results);
    }
}
