using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.UseCases.Admins.GrantPermission;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

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

    /// <summary>
    /// Initializes a new instance of <see cref="RevokeAdminPermissionCommandHandler"/>.
    /// </summary>
    public RevokeAdminPermissionCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<AdminPermissionResponseDto> Handle(RevokeAdminPermissionCommand request, CancellationToken cancellationToken)
    {
        // Verify caller is Super Admin
        var callerAdmin = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == request.SuperAdminUserId, cancellationToken);

        if (callerAdmin == null || callerAdmin.Role != AdminRoleType.SuperAdmin || !callerAdmin.IsActive)
        {
            throw new UnauthorizedAccessException("Only active Super Admins can revoke administrative permissions.");
        }

        var targetAdmin = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.Id == request.TargetAdminProfileId, cancellationToken)
            ?? throw new KeyNotFoundException($"Admin profile with ID {request.TargetAdminProfileId} not found.");

        targetAdmin.RevokePermission(request.Permission);

        // Add audit log entry
        _dbContext.AuditLogs.Add(new AuditLog(
            request.SuperAdminUserId,
            "Admin.RevokePermission",
            "AdminProfile",
            targetAdmin.Id.ToString(),
            $"Revoked permission: {request.Permission}"));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AdminPermissionResponseDto(
            targetAdmin.Id,
            targetAdmin.UserId,
            targetAdmin.Role.ToString(),
            targetAdmin.PermissionsList);
    }
}
