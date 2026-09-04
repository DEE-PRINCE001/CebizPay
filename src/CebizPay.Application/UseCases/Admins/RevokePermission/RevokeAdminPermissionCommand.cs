using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.UseCases.Admins.GrantPermission;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admins.RevokePermission;

/// <summary>
/// Command for Super Admin to revoke a delegated permission from an admin profile.
/// </summary>
public sealed record RevokeAdminPermissionCommand(
    string SuperAdminUserId,
    Guid TargetAdminProfileId,
    string Permission) : IRequest<AdminPermissionResponseDto>;

/// <summary>
/// Validator for RevokeAdminPermissionCommand.
/// </summary>
public sealed class RevokeAdminPermissionCommandValidator : AbstractValidator<RevokeAdminPermissionCommand>
{
    /// <summary>
    /// Initializes validation rules for RevokeAdminPermissionCommand.
    /// </summary>
    public RevokeAdminPermissionCommandValidator()
    {
        RuleFor(x => x.SuperAdminUserId).NotEmpty().WithMessage("SuperAdminUserId is required.");
        RuleFor(x => x.TargetAdminProfileId).NotEmpty().WithMessage("TargetAdminProfileId is required.");
        RuleFor(x => x.Permission).NotEmpty().WithMessage("Permission is required.");
    }
}

/// <summary>
/// Handler for RevokeAdminPermissionCommand.
/// </summary>
public sealed class RevokeAdminPermissionCommandHandler : IRequestHandler<RevokeAdminPermissionCommand, AdminPermissionResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly CebizPay.Application.Common.Interfaces.Security.ICurrentUserService? _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="RevokeAdminPermissionCommandHandler"/>.
    /// </summary>
    public RevokeAdminPermissionCommandHandler(
        IApplicationDbContext dbContext,
        CebizPay.Application.Common.Interfaces.Security.ICurrentUserService? currentUserService = null)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    /// <inheritdoc/>
    public async Task<AdminPermissionResponseDto> Handle(RevokeAdminPermissionCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService?.UserId ?? request.SuperAdminUserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authenticated Super Admin user is required.");
        }

        // Verify caller is Super Admin
        var callerAdmin = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive, cancellationToken);

        if (callerAdmin == null || callerAdmin.Role != AdminRoleType.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Only active Super Admins can revoke administrative permissions.");
        }

        var targetAdmin = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.Id == request.TargetAdminProfileId, cancellationToken)
            ?? throw new KeyNotFoundException($"Admin profile with ID {request.TargetAdminProfileId} not found.");

        targetAdmin.RevokePermission(request.Permission);

        // Add audit log entry
        _dbContext.AuditLogs.Add(Domain.Entities.AuditLog.Create(
            actorId: callerUserId,
            action: Domain.Auditing.AuditActions.AdminPermissionRevoked,
            resourceType: Domain.Auditing.AuditResourceTypes.AdminProfile,
            resourceId: targetAdmin.Id.ToString(),
            afterJson: System.Text.Json.JsonSerializer.Serialize(new { Permission = request.Permission, TargetUserId = targetAdmin.UserId })));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AdminPermissionResponseDto(
            targetAdmin.Id,
            targetAdmin.UserId,
            targetAdmin.Role.ToString(),
            targetAdmin.PermissionsList);
    }
}
