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
/// Command for a Super Admin to toggle the active/inactive state of an administrative profile.
/// </summary>
public sealed record ToggleAdminStatusCommand(
    Guid AdminProfileId,
    bool IsActive) : IRequest<AdminProfileDto>;

/// <summary>
/// Validator for ToggleAdminStatusCommand.
/// </summary>
public sealed class ToggleAdminStatusCommandValidator : AbstractValidator<ToggleAdminStatusCommand>
{
    /// <summary>
    /// Initializes validation rules for ToggleAdminStatusCommand.
    /// </summary>
    public ToggleAdminStatusCommandValidator()
    {
        RuleFor(x => x.AdminProfileId)
            .NotEmpty().WithMessage("AdminProfileId is required.");
    }
}

/// <summary>
/// Handler for ToggleAdminStatusCommand.
/// </summary>
public sealed class ToggleAdminStatusCommandHandler : IRequestHandler<ToggleAdminStatusCommand, AdminProfileDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;

    /// <summary>
    /// Initializes a new instance of <see cref="ToggleAdminStatusCommandHandler"/>.
    /// </summary>
    public ToggleAdminStatusCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IIdentityService identityService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
    }

    /// <inheritdoc/>
    public async Task<AdminProfileDto> Handle(ToggleAdminStatusCommand request, CancellationToken cancellationToken)
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
            throw new UnauthorizedAccessException("Only active Super Admins can toggle administrative status.");
        }

        var targetAdmin = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.Id == request.AdminProfileId && !a.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Admin profile with ID '{request.AdminProfileId}' not found.");

        // Security invariant: Super Admins cannot deactivate their own profile
        if (targetAdmin.UserId == callerUserId && !request.IsActive)
        {
            throw new InvalidOperationException("Super Admins cannot deactivate their own administrative profile.");
        }

        // Security invariant: Prevent deactivating the last active Super Admin on the platform
        if (targetAdmin.Role == AdminRoleType.SuperAdmin && !request.IsActive)
        {
            var activeSuperAdminCount = await _dbContext.AdminProfiles
                .CountAsync(a => a.Role == AdminRoleType.SuperAdmin && a.IsActive && !a.IsDeleted, cancellationToken);

            if (activeSuperAdminCount <= 1)
            {
                throw new InvalidOperationException("Cannot deactivate the last active Super Admin on the platform.");
            }
        }

        if (request.IsActive)
        {
            targetAdmin.Activate();
        }
        else
        {
            targetAdmin.Deactivate();
        }

        // Record immutable audit log entry
        _dbContext.AuditLogs.Add(AuditLog.Create(
            actorId: callerUserId,
            action: AuditActions.AdminStatusChanged,
            resourceType: AuditResourceTypes.AdminProfile,
            resourceId: targetAdmin.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new
            {
                TargetUserId = targetAdmin.UserId,
                IsActive = request.IsActive,
                Role = targetAdmin.Role.ToString()
            })));

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Fetch user identity metadata
        var (found, _, email, phone) = await _identityService.FindUserByEmailAsync(targetAdmin.UserId, cancellationToken);
        if (!found)
        {
            var userDetailsMap = await _identityService.GetUserDetailsByIdsAsync(new[] { targetAdmin.UserId }, cancellationToken);
            if (userDetailsMap.TryGetValue(targetAdmin.UserId, out var details))
            {
                email = details.Email;
                phone = details.PhoneNumber;
            }
        }

        return new AdminProfileDto(
            targetAdmin.Id,
            targetAdmin.UserId,
            email,
            phone,
            targetAdmin.Role.ToString(),
            targetAdmin.IsActive,
            targetAdmin.IsMfaEnabled,
            targetAdmin.PermissionsList,
            targetAdmin.CreatedAtUtc,
            targetAdmin.UpdatedAtUtc);
    }
}
