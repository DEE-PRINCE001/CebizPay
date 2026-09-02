using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
/// Command to redeem an administrative invitation token and activate an admin profile.
/// </summary>
public sealed record RedeemAdminInviteCommand(
    string InvitationToken,
    string Password,
    string? PhoneNumber = null) : IRequest<RedeemAdminInviteResponseDto>;

/// <summary>
/// Validator for RedeemAdminInviteCommand.
/// </summary>
public sealed class RedeemAdminInviteCommandValidator : AbstractValidator<RedeemAdminInviteCommand>
{
    /// <summary>
    /// Initializes validation rules for RedeemAdminInviteCommand.
    /// </summary>
    public RedeemAdminInviteCommandValidator()
    {
        RuleFor(x => x.InvitationToken)
            .NotEmpty().WithMessage("Invitation token is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Admin password must be at least 8 characters long.");
    }
}

/// <summary>
/// Handler for RedeemAdminInviteCommand.
/// </summary>
public sealed class RedeemAdminInviteCommandHandler : IRequestHandler<RedeemAdminInviteCommand, RedeemAdminInviteResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IIdentityService _identityService;

    /// <summary>
    /// Initializes a new instance of <see cref="RedeemAdminInviteCommandHandler"/>.
    /// </summary>
    public RedeemAdminInviteCommandHandler(
        IApplicationDbContext dbContext,
        IIdentityService identityService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
    }

    /// <inheritdoc/>
    public async Task<RedeemAdminInviteResponseDto> Handle(RedeemAdminInviteCommand request, CancellationToken cancellationToken)
    {
        var rawToken = request.InvitationToken.Trim();
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

        // Retrieve invitation by token hash
        var invitation = await _dbContext.AdminInvitations
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

        if (invitation == null || invitation.Status != AdminInvitationStatus.Pending)
        {
            throw new InvalidOperationException("Invalid, cancelled, or already redeemed invitation token.");
        }

        var now = DateTime.UtcNow;
        if (invitation.IsExpired(now))
        {
            invitation.MarkExpired();
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new InvalidOperationException("Invitation token has expired. Please request a new invitation from a Super Admin.");
        }

        // Check if identity user already exists or register a new identity user
        var (found, existingUserId, _, _) = await _identityService.FindUserByEmailAsync(invitation.Email, cancellationToken);
        string userId;

        if (found && !string.IsNullOrWhiteSpace(existingUserId))
        {
            userId = existingUserId;
        }
        else
        {
            var (succeeded, newUserId, errors) = await _identityService.RegisterUserAsync(
                invitation.Email,
                request.Password,
                request.PhoneNumber,
                cancellationToken);

            if (!succeeded)
            {
                return new RedeemAdminInviteResponseDto(
                    false,
                    null,
                    invitation.Email,
                    invitation.Role.ToString(),
                    null,
                    null,
                    errors);
            }

            userId = newUserId;
        }

        // Check if AdminProfile already exists for this user ID
        var existingProfile = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

        if (existingProfile != null)
        {
            if (!existingProfile.IsDeleted)
            {
                throw new InvalidOperationException("An active administrative profile already exists for this identity user.");
            }

            // If previously soft-deleted, reactivate and update role to the invited role
            existingProfile.ChangeRole(invitation.Role);
            existingProfile.Activate();
        }
        else
        {
            var newProfile = new AdminProfile(userId, invitation.Role, isMfaEnabled: false);
            _dbContext.AdminProfiles.Add(newProfile);
        }

        // Mark invitation redeemed
        invitation.Redeem(userId, now);

        // Record audit log entry
        _dbContext.AuditLogs.Add(AuditLog.Create(
            actorId: userId,
            action: AuditActions.AdminInviteRedeemed,
            resourceType: AuditResourceTypes.AdminInvitation,
            resourceId: invitation.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new
            {
                UserId = userId,
                Email = invitation.Email,
                Role = invitation.Role.ToString(),
                RedeemedAtUtc = now
            })));

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Issue authentication tokens for immediate login
        var (accessToken, refreshToken) = await _identityService.IssueTokensForUserAsync(userId, cancellationToken);

        return new RedeemAdminInviteResponseDto(
            true,
            userId,
            invitation.Email,
            invitation.Role.ToString(),
            accessToken,
            refreshToken,
            null);
    }
}
