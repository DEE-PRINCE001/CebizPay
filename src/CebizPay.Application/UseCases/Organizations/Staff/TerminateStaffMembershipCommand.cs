using CebizPay.Application.Common.Interfaces.Loans;
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
/// Command to terminate a staff member's work relationship with an organization and convert corporate payroll loans to standard loans.
/// </summary>
public sealed record TerminateStaffMembershipCommand(
    Guid OrganizationId,
    Guid MembershipId,
    string Reason) : IRequest<bool>;

/// <summary>
/// Validator for TerminateStaffMembershipCommand.
/// </summary>
public sealed class TerminateStaffMembershipCommandValidator : AbstractValidator<TerminateStaffMembershipCommand>
{
    /// <summary>
    /// Initializes validation rules for TerminateStaffMembershipCommand.
    /// </summary>
    public TerminateStaffMembershipCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.MembershipId).NotEmpty().WithMessage("MembershipId is required.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Termination reason is required.").MaximumLength(500);
    }
}

/// <summary>
/// Handler for TerminateStaffMembershipCommand.
/// </summary>
public sealed class TerminateStaffMembershipCommandHandler : IRequestHandler<TerminateStaffMembershipCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILoanContractService _loanContractService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="TerminateStaffMembershipCommandHandler"/>.
    /// </summary>
    public TerminateStaffMembershipCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService,
        ILoanContractService loanContractService,
        IOutboxService outboxService)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _currentUserService = currentUserService;
        _loanContractService = loanContractService;
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public async Task<bool> Handle(TerminateStaffMembershipCommand request, CancellationToken cancellationToken)
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

        if (membership.Status == MembershipStatus.Terminated)
        {
            throw new InvalidOperationException("Staff member is already terminated.");
        }

        var beforeJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            membership.Id,
            Status = membership.Status.ToString()
        });

        membership.TerminateWorkAccess(request.Reason);

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";

        // Execute corporate payroll loan offboarding conversion via existing LoanContractService
        await _loanContractService.ConvertTerminatedStaffLoansAsync(
            request.OrganizationId,
            membership.UserId,
            request.Reason,
            actorUserId,
            cancellationToken);

        // Check if user has active memberships in any other organization
        var hasOtherActiveMemberships = await _dbContext.OrganizationMemberships
            .AnyAsync(m => m.UserId == membership.UserId && m.Id != membership.Id && m.Status == MembershipStatus.Active, cancellationToken);

        if (!hasOtherActiveMemberships)
        {
            var profile = await _dbContext.IndividualProfiles.FirstOrDefaultAsync(
                p => p.UserId == membership.UserId,
                cancellationToken);

            profile?.UpdateProfessionalStatus(ProfessionalStatus.NotAStaff);
        }

        var afterJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            membership.Id,
            Status = membership.Status.ToString(),
            membership.SuspendedAtUtc,
            membership.SuspensionReason
        });

        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.StaffTerminated,
            resourceType: AuditResourceTypes.StaffMember,
            resourceId: membership.Id.ToString(),
            organizationId: request.OrganizationId,
            beforeJson: beforeJson,
            afterJson: afterJson);
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new StaffMembershipTerminatedDomainEvent(
            membership.Id,
            request.OrganizationId,
            membership.UserId,
            request.Reason,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
