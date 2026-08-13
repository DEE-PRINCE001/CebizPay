using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Application.UseCases.StaffInvitations.InviteStaff;

/// <summary>
/// Command to invite a staff member to an organization.
/// </summary>
/// <param name="OrganizationId">Target organization ID.</param>
/// <param name="TargetEmail">Email address of person invited.</param>
public sealed record InviteStaffCommand(
    Guid OrganizationId,
    string TargetEmail) : IRequest<InviteStaffResponseDto>;

/// <summary>
/// Response DTO for staff invitation.
/// </summary>
public sealed record InviteStaffResponseDto(
    Guid InvitationId,
    Guid OrganizationId,
    string TargetEmail,
    string InvitationCode,
    DateTime ExpiresAtUtc);

/// <summary>
/// Validator for InviteStaffCommand.
/// </summary>
public sealed class InviteStaffCommandValidator : AbstractValidator<InviteStaffCommand>
{
    /// <summary>
    /// Initializes validation rules for InviteStaffCommand.
    /// </summary>
    public InviteStaffCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.TargetEmail).NotEmpty().WithMessage("TargetEmail is required.").EmailAddress();
    }
}

/// <summary>
/// Handler for InviteStaffCommand.
/// </summary>
public sealed class InviteStaffCommandHandler : IRequestHandler<InviteStaffCommand, InviteStaffResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly IEventPublisher _eventPublisher;

    /// <summary>
    /// Initializes a new instance of <see cref="InviteStaffCommandHandler"/>.
    /// </summary>
    public InviteStaffCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        IEventPublisher eventPublisher)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _eventPublisher = eventPublisher;
    }

    /// <inheritdoc/>
    public async Task<InviteStaffResponseDto> Handle(InviteStaffCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization {request.OrganizationId} not found.");

        var invitation = new StaffInvitation(org.Id, request.TargetEmail);
        _dbContext.StaffInvitations.Add(invitation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(
            new StaffInvitationCreatedDomainEvent(
                invitation.Id, org.Id, invitation.Email, invitation.InvitationCode, invitation.ExpiresAtUtc, DateTime.UtcNow),
            cancellationToken);

        return new InviteStaffResponseDto(
            invitation.Id, org.Id, invitation.Email, invitation.InvitationCode, invitation.ExpiresAtUtc);
    }
}
