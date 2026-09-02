using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Events;
using FluentValidation;
using MediatR;

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
    private readonly IEmailService? _emailService;

    /// <summary>
    /// Initializes a new instance of <see cref="InviteStaffCommandHandler"/> with email service.
    /// </summary>
    public InviteStaffCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        IEventPublisher eventPublisher,
        IEmailService emailService)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _eventPublisher = eventPublisher;
        _emailService = emailService;
    }

    /// <summary>
    /// Backward-compatible constructor for testing.
    /// </summary>
    public InviteStaffCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        IEventPublisher eventPublisher)
        : this(dbContext, orgContext, eventPublisher, null!)
    {
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

        if (_emailService != null)
        {
            var orgName = org.CompanyName;
            var subject = $"Invitation to join {orgName} on CebizPay";
            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f8fafc; color: #1e293b; padding: 24px; }}
        .container {{ max-width: 540px; margin: 0 auto; background: #ffffff; border-radius: 12px; border: 1px solid #e2e8f0; padding: 32px; }}
        .header {{ font-size: 20px; font-weight: 700; color: #0f172a; margin-bottom: 16px; }}
        .code-box {{ background-color: #f1f5f9; border-radius: 8px; font-size: 28px; font-weight: 800; letter-spacing: 4px; text-align: center; padding: 16px; margin: 24px 0; color: #0284c7; border: 1px dashed #cbd5e1; }}
        .footer {{ font-size: 12px; color: #64748b; margin-top: 32px; border-top: 1px solid #f1f5f9; padding-top: 16px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>Staff Invitation</div>
        <p>You have been invited to join <strong>{orgName}</strong> on the CebizPay platform.</p>
        <p>Your unique invitation code is:</p>
        <div class='code-box'>{invitation.InvitationCode}</div>
        <p>Use this code during registration or acceptance to join the organization workspace.</p>
        <div class='footer'>&copy; {DateTime.UtcNow.Year} CebizPay. All rights reserved.</div>
    </div>
</body>
</html>";
            var plainText = $"You have been invited to join {orgName} on CebizPay. Your invitation code is: {invitation.InvitationCode}";
            await _emailService.SendEmailAsync(invitation.Email, subject, htmlBody, plainText, toName: null, cancellationToken).ConfigureAwait(false);
        }

        return new InviteStaffResponseDto(
            invitation.Id, org.Id, invitation.Email, invitation.InvitationCode, invitation.ExpiresAtUtc);
    }
}
