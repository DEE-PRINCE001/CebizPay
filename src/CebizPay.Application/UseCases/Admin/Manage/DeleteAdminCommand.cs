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
/// Command for a Super Admin to soft-delete / archive an administrative profile.
/// </summary>
public sealed record DeleteAdminCommand(
    Guid AdminProfileId) : IRequest<bool>;

/// <summary>
/// Validator for DeleteAdminCommand.
/// </summary>
public sealed class DeleteAdminCommandValidator : AbstractValidator<DeleteAdminCommand>
{
    /// <summary>
    /// Initializes validation rules for DeleteAdminCommand.
    /// </summary>
    public DeleteAdminCommandValidator()
    {
        RuleFor(x => x.AdminProfileId)
            .NotEmpty().WithMessage("AdminProfileId is required.");
    }
}

/// <summary>
/// Handler for DeleteAdminCommand.
/// </summary>
public sealed class DeleteAdminCommandHandler : IRequestHandler<DeleteAdminCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="DeleteAdminCommandHandler"/>.
    /// </summary>
    public DeleteAdminCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<bool> Handle(DeleteAdminCommand request, CancellationToken cancellationToken)
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
            throw new UnauthorizedAccessException("Only active Super Admins can delete administrative users.");
        }

        var targetAdmin = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.Id == request.AdminProfileId && !a.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Admin profile with ID '{request.AdminProfileId}' not found.");

        // Security invariant: Super Admins cannot delete their own profile
        if (targetAdmin.UserId == callerUserId)
        {
            throw new InvalidOperationException("Super Admins cannot delete their own administrative profile.");
        }

        // Security invariant: Prevent deleting the last active Super Admin on the platform
        if (targetAdmin.Role == AdminRoleType.SuperAdmin)
        {
            var activeSuperAdminCount = await _dbContext.AdminProfiles
                .CountAsync(a => a.Role == AdminRoleType.SuperAdmin && a.IsActive && !a.IsDeleted, cancellationToken);

            if (activeSuperAdminCount <= 1)
            {
                throw new InvalidOperationException("Cannot delete the last active Super Admin on the platform.");
            }
        }

        var now = DateTime.UtcNow;
        targetAdmin.SoftDelete(callerUserId, now);

        // Record immutable audit log entry
        _dbContext.AuditLogs.Add(AuditLog.Create(
            actorId: callerUserId,
            action: AuditActions.AdminDeleted,
            resourceType: AuditResourceTypes.AdminProfile,
            resourceId: targetAdmin.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new
            {
                TargetUserId = targetAdmin.UserId,
                Role = targetAdmin.Role.ToString(),
                DeletedAtUtc = now
            })));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
