using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Application.UseCases.StaffInvitations.SuspendStaff;

/// <summary>
/// Command to suspend a staff member's work access within an organization.
/// </summary>
/// <param name="MembershipId">Organization membership ID.</param>
/// <param name="Reason">Reason for suspension.</param>
public sealed record SuspendStaffMembershipCommand(
    Guid MembershipId,
    string Reason) : IRequest<SuspendStaffMembershipResponseDto>;

/// <summary>
/// Response DTO for staff suspension.
/// </summary>
public sealed record SuspendStaffMembershipResponseDto(
    Guid MembershipId,
    Guid OrganizationId,
    string UserId,
    string Status,
    string Reason);

/// <summary>
/// Validator for SuspendStaffMembershipCommand.
/// </summary>
public sealed class SuspendStaffMembershipCommandValidator : AbstractValidator<SuspendStaffMembershipCommand>
{
    /// <summary>
    /// Initializes validation rules for SuspendStaffMembershipCommand.
    /// </summary>
    public SuspendStaffMembershipCommandValidator()
    {
        RuleFor(x => x.MembershipId).NotEmpty().WithMessage("MembershipId is required.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Suspension Reason is required.");
    }
}

/// <summary>
/// Handler for SuspendStaffMembershipCommand.
/// </summary>
public sealed class SuspendStaffMembershipCommandHandler : IRequestHandler<SuspendStaffMembershipCommand, SuspendStaffMembershipResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly IEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of <see cref="SuspendStaffMembershipCommandHandler"/>.
    /// </summary>
    public SuspendStaffMembershipCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        IEventPublisher eventPublisher)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc/>
    public async Task<SuspendStaffMembershipResponseDto> Handle(SuspendStaffMembershipCommand request, CancellationToken cancellationToken)
    {
        var membership = await _dbContext.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.Id == request.MembershipId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization membership with ID {request.MembershipId} not found.");

        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(membership.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {membership.OrganizationId}.");
        }

        // Suspend staff work relationship for this organization ONLY
        membership.SuspendWorkAccess(request.Reason);

        // Check if user has any remaining active staff memberships elsewhere
        var hasOtherActiveMemberships = await _dbContext.OrganizationMemberships
            .AnyAsync(m => m.UserId == membership.UserId && m.Id != membership.Id && m.Status == MembershipStatus.Active, cancellationToken);

        if (!hasOtherActiveMemberships)
        {
            var profile = await _dbContext.IndividualProfiles
                .FirstOrDefaultAsync(p => p.UserId == membership.UserId, cancellationToken);
            profile?.UpdateProfessionalStatus(ProfessionalStatus.NotAStaff);
        }

        // NOTE: Individual Profile's KycStatus, global Identity, personal wallet, personal savings/thrift are NOT modified or suspended!

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(
            new StaffMembershipSuspendedDomainEvent(
                membership.Id, membership.OrganizationId, membership.UserId, request.Reason, DateTime.UtcNow),
            cancellationToken);

        return new SuspendStaffMembershipResponseDto(
            membership.Id, membership.OrganizationId, membership.UserId, membership.Status.ToString(), request.Reason);
    }
}
