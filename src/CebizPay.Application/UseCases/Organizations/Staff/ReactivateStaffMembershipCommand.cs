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
/// Command to reactivate a suspended or terminated staff membership within an organization.
/// </summary>
public sealed record ReactivateStaffMembershipCommand(
    Guid OrganizationId,
    Guid MembershipId) : IRequest<bool>;

/// <summary>
/// Validator for ReactivateStaffMembershipCommand.
/// </summary>
public sealed class ReactivateStaffMembershipCommandValidator : AbstractValidator<ReactivateStaffMembershipCommand>
{
    /// <summary>
    /// Initializes validation rules for ReactivateStaffMembershipCommand.
    /// </summary>
    public ReactivateStaffMembershipCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.MembershipId).NotEmpty().WithMessage("MembershipId is required.");
    }
}

/// <summary>
/// Handler for ReactivateStaffMembershipCommand.
/// </summary>
public sealed class ReactivateStaffMembershipCommandHandler : IRequestHandler<ReactivateStaffMembershipCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="ReactivateStaffMembershipCommandHandler"/>.
    /// </summary>
    public ReactivateStaffMembershipCommandHandler(
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
    public async Task<bool> Handle(ReactivateStaffMembershipCommand request, CancellationToken cancellationToken)
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

        var membership = await _dbContext.OrganizationMemberships.FirstOrDefaultAsync(
            m => m.Id == request.MembershipId && m.OrganizationId == request.OrganizationId,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Staff membership {request.MembershipId} not found in organization {request.OrganizationId}.");

        if (membership.Status == MembershipStatus.Active)
        {
            return true;
        }

        var beforeJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            membership.Id,
            Status = membership.Status.ToString(),
            membership.SuspendedAtUtc,
            membership.SuspensionReason
        });

        membership.ReactivateWorkAccess();

        var profile = await _dbContext.IndividualProfiles.FirstOrDefaultAsync(
            p => p.UserId == membership.UserId,
            cancellationToken);

        if (profile != null && profile.ProfessionalStatus != ProfessionalStatus.Staff)
        {
            profile.UpdateProfessionalStatus(ProfessionalStatus.Staff);
        }

        var afterJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            membership.Id,
            Status = membership.Status.ToString()
        });

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.StaffReactivated,
            resourceType: AuditResourceTypes.StaffMember,
            resourceId: membership.Id.ToString(),
            organizationId: request.OrganizationId,
            beforeJson: beforeJson,
            afterJson: afterJson);
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new StaffMembershipReactivatedDomainEvent(
            membership.Id,
            request.OrganizationId,
            membership.UserId,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
