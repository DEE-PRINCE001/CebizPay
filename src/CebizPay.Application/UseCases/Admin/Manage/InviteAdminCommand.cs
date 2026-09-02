using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Application.Common.Extensions;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Manage;

/// <summary>
/// Command for a Super Admin to issue a single-use 24-hour invitation for a new administrative user.
/// </summary>
public sealed record InviteAdminCommand(
    string Email,
    AdminRoleType Role) : IRequest<InviteAdminResponseDto>;

/// <summary>
/// Validator for InviteAdminCommand.
/// </summary>
public sealed class InviteAdminCommandValidator : AbstractValidator<InviteAdminCommand>
{
    /// <summary>
    /// Initializes validation rules for InviteAdminCommand.
    /// </summary>
    public InviteAdminCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("A valid administrative role must be specified.");
    }
}

/// <summary>
/// Handler for InviteAdminCommand.
/// </summary>
public sealed class InviteAdminCommandHandler : IRequestHandler<InviteAdminCommand, InviteAdminResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;
    private readonly IEmailService? _emailService;

    /// <summary>
    /// Initializes a new instance of <see cref="InviteAdminCommandHandler"/>.
    /// </summary>
    public InviteAdminCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IIdentityService identityService,
        IEmailService? emailService = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        _emailService = emailService;
    }

    /// <inheritdoc/>
    public async Task<InviteAdminResponseDto> Handle(InviteAdminCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        // Authorize caller is an active, non-deleted Super Admin
        var callerAdmin = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive, cancellationToken);

        if (callerAdmin == null || callerAdmin.Role != AdminRoleType.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Only active Super Admins can invite administrative users.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Check if an existing identity user is already an active non-deleted admin
        var (found, existingUserId, _, _) = await _identityService.FindUserByEmailAsync(normalizedEmail, cancellationToken);
        if (found && !string.IsNullOrWhiteSpace(existingUserId))
        {
            var existingProfile = await _dbContext.AdminProfiles
                .FirstOrDefaultAsync(a => a.UserId == existingUserId && !a.IsDeleted, cancellationToken);

            if (existingProfile != null)
            {
                throw new InvalidOperationException($"An administrative user with email '{normalizedEmail}' already exists.");
            }
        }

        // Invalidate/cancel any previous pending invitations for this email to prevent duplicate active tokens
        var existingPendingInvites = await _dbContext.AdminInvitations
            .Where(i => i.Email == normalizedEmail && i.Status == AdminInvitationStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var pendingInvite in existingPendingInvites)
        {
            pendingInvite.Cancel(DateTime.UtcNow);
        }

        // Generate high-entropy 256-bit cryptographic token (64 hex characters)
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToHexString(tokenBytes).ToLowerInvariant();
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

        var invitation = new AdminInvitation(
            normalizedEmail,
            request.Role,
            tokenHash,
            callerUserId,
            TimeSpan.FromHours(24));

        _dbContext.AdminInvitations.Add(invitation);

        // Record immutable audit log entry (raw token is never persisted in audit trail)
        _dbContext.AuditLogs.Add(AuditLog.Create(
            actorId: callerUserId,
            action: AuditActions.AdminInvited,
            resourceType: AuditResourceTypes.AdminInvitation,
            resourceId: invitation.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new
            {
                Email = normalizedEmail,
                Role = request.Role.ToString(),
                ExpiresAtUtc = invitation.ExpiresAtUtc
            })));

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Dispatch transactional email via IEmailService
        if (_emailService != null)
        {
            var subject = "Invitation to Join CebizPay Platform Administration";
            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f8fafc; color: #1e293b; padding: 24px; }}
        .container {{ max-width: 540px; margin: 0 auto; background: #ffffff; border-radius: 12px; border: 1px solid #e2e8f0; padding: 32px; }}
        .header {{ font-size: 20px; font-weight: 700; color: #0f172a; margin-bottom: 16px; }}
        .code-box {{ background-color: #f1f5f9; border-radius: 8px; font-size: 16px; font-weight: 700; word-break: break-all; text-align: center; padding: 16px; margin: 24px 0; color: #0284c7; border: 1px dashed #cbd5e1; }}
        .footer {{ font-size: 12px; color: #64748b; margin-top: 32px; border-top: 1px solid #f1f5f9; padding-top: 16px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>CebizPay Administrative Invitation</div>
        <p>You have been invited to join the CebizPay Control Plane as an <strong>{request.Role}</strong>.</p>
        <p>Use the single-use invitation token below to activate your administrative profile within 24 hours:</p>
        <div class='code-box'>{rawToken}</div>
        <p style='color: #64748b; font-size: 14px;'>This token will expire at <strong>{invitation.ExpiresAtUtc:yyyy-MM-dd HH:mm:ss} UTC</strong>.</p>
        <div class='footer'>If you did not expect this invitation, please ignore this email or notify platform security.</div>
    </div>
</body>
</html>";
            await _emailService.SendEmailAsync(
                normalizedEmail,
                subject,
                htmlBody,
                plainTextBody: $"Your CebizPay Admin invitation token is: {rawToken}. It expires at {invitation.ExpiresAtUtc:u} UTC.",
                toName: "Platform Administrator",
                cancellationToken: cancellationToken);
        }

        return new InviteAdminResponseDto(
            invitation.Id,
            invitation.Email,
            invitation.Role.ToString(),
            rawToken,
            invitation.ExpiresAtUtc);
    }
}
