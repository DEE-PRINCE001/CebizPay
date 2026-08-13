using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Application.UseCases.Admins.GrantPermission;

/// <summary>
/// Command for Super Admin to grant a delegated permission to an admin profile.
/// </summary>
public sealed record GrantAdminPermissionCommand(
    string SuperAdminUserId,
    Guid TargetAdminProfileId,
    string Permission) : IRequest<AdminPermissionResponseDto>;

/// <summary>
/// Response DTO for admin permission operations.
/// </summary>
public sealed record AdminPermissionResponseDto(
    Guid AdminProfileId,
    string UserId,
    string Role,
    IReadOnlyList<string> Permissions);

/// <summary>
/// Validator for GrantAdminPermissionCommand.
/// </summary>
public sealed class GrantAdminPermissionCommandValidator : AbstractValidator<GrantAdminPermissionCommand>
{
    /// <summary>
    /// Initializes validation rules for GrantAdminPermissionCommand.
    /// </summary>
    public GrantAdminPermissionCommandValidator()
    {
        RuleFor(x => x.SuperAdminUserId).NotEmpty().WithMessage("SuperAdminUserId is required.");
        RuleFor(x => x.TargetAdminProfileId).NotEmpty().WithMessage("TargetAdminProfileId is required.");
        RuleFor(x => x.Permission).NotEmpty().WithMessage("Permission is required.");
    }
}

/// <summary>
/// Handler for GrantAdminPermissionCommand.
/// </summary>
public sealed class GrantAdminPermissionCommandHandler : IRequestHandler<GrantAdminPermissionCommand, AdminPermissionResponseDto>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GrantAdminPermissionCommandHandler"/>.
    /// </summary>
    public GrantAdminPermissionCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<AdminPermissionResponseDto> Handle(GrantAdminPermissionCommand request, CancellationToken cancellationToken)
    {
        // Verify caller is Super Admin
        var callerAdmin = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == request.SuperAdminUserId, cancellationToken);

        if (callerAdmin == null || callerAdmin.Role != AdminRoleType.SuperAdmin || !callerAdmin.IsActive)
        {
            throw new UnauthorizedAccessException("Only active Super Admins can grant administrative permissions.");
        }

        var targetAdmin = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.Id == request.TargetAdminProfileId, cancellationToken)
            ?? throw new KeyNotFoundException($"Admin profile with ID {request.TargetAdminProfileId} not found.");

        if (targetAdmin.UserId == request.SuperAdminUserId)
        {
            throw new InvalidOperationException("Super Admins cannot modify their own permission assignments via delegation.");
        }

        targetAdmin.GrantPermission(request.Permission);

        // Add audit log entry
        _dbContext.AuditLogs.Add(new AuditLog(
            request.SuperAdminUserId,
            "Admin.GrantPermission",
            "AdminProfile",
            targetAdmin.Id.ToString(),
            $"Granted permission: {request.Permission}"));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AdminPermissionResponseDto(
            targetAdmin.Id,
            targetAdmin.UserId,
            targetAdmin.Role.ToString(),
            targetAdmin.PermissionsList);
    }
}
