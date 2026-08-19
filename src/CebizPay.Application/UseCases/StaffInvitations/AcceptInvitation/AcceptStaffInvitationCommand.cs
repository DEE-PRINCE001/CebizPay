using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.StaffInvitations.AcceptInvitation;

/// <summary>
/// Command to accept a staff invitation and join an organization.
/// </summary>
/// <param name="InvitationCode">Invitation code string.</param>
/// <param name="UserId">ID of user accepting invitation.</param>
public sealed record AcceptStaffInvitationCommand(
    string InvitationCode,
    string UserId) : IRequest<AcceptStaffInvitationResponseDto>;

/// <summary>
/// Response DTO for invitation acceptance.
/// </summary>
public sealed record AcceptStaffInvitationResponseDto(
    Guid MembershipId,
    Guid OrganizationId,
    string UserId,
    string Status);

/// <summary>
/// Validator for AcceptStaffInvitationCommand.
/// </summary>
public sealed class AcceptStaffInvitationCommandValidator : AbstractValidator<AcceptStaffInvitationCommand>
{
    /// <summary>
    /// Initializes validation rules for AcceptStaffInvitationCommand.
    /// </summary>
    public AcceptStaffInvitationCommandValidator()
    {
        RuleFor(x => x.InvitationCode).NotEmpty().WithMessage("Invitation code is required.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required.");
    }
}

/// <summary>
/// Handler for AcceptStaffInvitationCommand.
/// </summary>
public sealed class AcceptStaffInvitationCommandHandler : IRequestHandler<AcceptStaffInvitationCommand, AcceptStaffInvitationResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of <see cref="AcceptStaffInvitationCommandHandler"/>.
    /// </summary>
    public AcceptStaffInvitationCommandHandler(IApplicationDbContext dbContext, IEventPublisher eventPublisher)
    {
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc/>
    public async Task<AcceptStaffInvitationResponseDto> Handle(AcceptStaffInvitationCommand request, CancellationToken cancellationToken)
    {
        var targetCode = request.InvitationCode.Trim().ToUpperInvariant();

        var invitation = await _dbContext.StaffInvitations
            .FirstOrDefaultAsync(i => i.InvitationCode == targetCode, cancellationToken)
            ?? throw new KeyNotFoundException("Staff invitation not found or code is invalid.");

        if (invitation.IsExpired(DateTime.UtcNow))
        {
            throw new InvalidOperationException("Invitation has expired or is no longer pending.");
        }

        // Mandatory Rule (Section 10 of todo.md): Individual user MUST have VERIFIED KYC to accept staff invitation.
        var profile = await _dbContext.IndividualProfiles
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException($"Individual profile for user {request.UserId} not found.");

        if (!profile.CanAcceptStaffInvitation())
        {
            throw new InvalidOperationException("Cannot accept staff invitation. Individual KYC status must be VERIFIED.");
        }

        // Check duplicate active membership
        var existingMembership = await _dbContext.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.UserId == request.UserId && m.OrganizationId == invitation.OrganizationId, cancellationToken);

        if (existingMembership != null && existingMembership.Status == MembershipStatus.Active)
        {
            throw new InvalidOperationException("User is already an active staff member of this organization.");
        }

        invitation.Accept(DateTime.UtcNow);

        OrganizationMembership membership;
        if (existingMembership != null)
        {
            existingMembership.ReactivateWorkAccess();
            membership = existingMembership;
        }
        else
        {
            membership = new OrganizationMembership(request.UserId, invitation.OrganizationId, MembershipRoleType.Member);
            _dbContext.OrganizationMemberships.Add(membership);
        }

        profile.UpdateProfessionalStatus(ProfessionalStatus.Staff);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(
            new StaffInvitationAcceptedDomainEvent(
                invitation.Id, invitation.OrganizationId, request.UserId, membership.Id, DateTime.UtcNow),
            cancellationToken);

        return new AcceptStaffInvitationResponseDto(
            membership.Id, invitation.OrganizationId, request.UserId, membership.Status.ToString());
    }
}
