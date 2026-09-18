using System.Text.Json;
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Individuals;

/// <summary>
/// Command to administratively suspend an individual profile.
/// </summary>
public sealed record SuspendIndividualCommand(
    string Id,
    string Reason,
    string AdminUserId) : IRequest<AdminIndividualStatusResultDto>;

/// <summary>
/// Validator for SuspendIndividualCommand.
/// </summary>
public sealed class SuspendIndividualCommandValidator : AbstractValidator<SuspendIndividualCommand>
{
    /// <summary>
    /// Initializes validation rules for SuspendIndividualCommand.
    /// </summary>
    public SuspendIndividualCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Individual ID is required.");

        RuleFor(x => x.AdminUserId)
            .NotEmpty().WithMessage("Admin user ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Suspension reason is required.")
            .MinimumLength(5).WithMessage("Suspension reason must be at least 5 characters.")
            .MaximumLength(500).WithMessage("Suspension reason must not exceed 500 characters.");

        RuleFor(x => x)
            .Must(x => x.AdminUserId != x.Id)
            .WithMessage("Administrators cannot suspend their own individual account.");
    }
}

/// <summary>
/// Handler for SuspendIndividualCommand.
/// </summary>
public sealed class SuspendIndividualCommandHandler : IRequestHandler<SuspendIndividualCommand, AdminIndividualStatusResultDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICurrentUserService? _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="SuspendIndividualCommandHandler"/>.
    /// </summary>
    public SuspendIndividualCommandHandler(
        IApplicationDbContext dbContext,
        IEventPublisher eventPublisher,
        ICurrentUserService? currentUserService = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _currentUserService = currentUserService;
    }

    /// <inheritdoc/>
    public async Task<AdminIndividualStatusResultDto> Handle(SuspendIndividualCommand request, CancellationToken cancellationToken)
    {
        var effectiveAdminUserId = _currentUserService?.UserId ?? request.AdminUserId;
        if (string.IsNullOrWhiteSpace(effectiveAdminUserId))
        {
            throw new UnauthorizedAccessException("Authenticated admin user is required.");
        }

        var adminProfile = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == effectiveAdminUserId && !a.IsDeleted && a.IsActive, cancellationToken);

        if (adminProfile == null || (adminProfile.Role != AdminRoleType.SuperAdmin && adminProfile.Role != AdminRoleType.Admin))
        {
            throw new UnauthorizedAccessException("User is not authorized to suspend individual profiles.");
        }

        var trimmedId = request.Id.Trim();
        var isGuid = Guid.TryParse(trimmedId, out var parsedGuid);

        var profile = await _dbContext.IndividualProfiles
            .FirstOrDefaultAsync(p => (isGuid && p.Id == parsedGuid) || p.UserId == trimmedId, cancellationToken)
            ?? throw new KeyNotFoundException($"Individual with identifier '{request.Id}' was not found.");

        if (effectiveAdminUserId == profile.UserId)
        {
            throw new InvalidOperationException("Administrators cannot suspend their own individual account.");
        }

        profile.Suspend(request.Reason);

        _dbContext.AuditLogs.Add(AuditLog.Create(
            actorId: effectiveAdminUserId,
            action: AuditActions.IndividualSuspended,
            resourceType: AuditResourceTypes.User,
            resourceId: profile.UserId,
            afterJson: JsonSerializer.Serialize(new
            {
                Reason = request.Reason,
                ProfileId = profile.Id,
                IsSuspended = profile.IsSuspended,
                SuspendedAtUtc = profile.SuspendedAtUtc
            })));

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(
            new IndividualSuspendedDomainEvent(
                profile.Id,
                profile.UserId,
                request.Reason,
                effectiveAdminUserId,
                DateTime.UtcNow),
            cancellationToken);

        return new AdminIndividualStatusResultDto(
            profile.Id,
            profile.UserId,
            "Suspended",
            profile.IsSuspended,
            profile.SuspendedAtUtc,
            profile.SuspensionReason);
    }
}

/// <summary>
/// Command to administratively reactivate a suspended individual profile.
/// </summary>
public sealed record ReactivateIndividualCommand(
    string Id,
    string Reason,
    string AdminUserId) : IRequest<AdminIndividualStatusResultDto>;

/// <summary>
/// Validator for ReactivateIndividualCommand.
/// </summary>
public sealed class ReactivateIndividualCommandValidator : AbstractValidator<ReactivateIndividualCommand>
{
    /// <summary>
    /// Initializes validation rules for ReactivateIndividualCommand.
    /// </summary>
    public ReactivateIndividualCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Individual ID is required.");

        RuleFor(x => x.AdminUserId)
            .NotEmpty().WithMessage("Admin user ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reactivation reason is required.")
            .MinimumLength(5).WithMessage("Reactivation reason must be at least 5 characters.")
            .MaximumLength(500).WithMessage("Reactivation reason must not exceed 500 characters.");
    }
}

/// <summary>
/// Handler for ReactivateIndividualCommand.
/// </summary>
public sealed class ReactivateIndividualCommandHandler : IRequestHandler<ReactivateIndividualCommand, AdminIndividualStatusResultDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher;
    private readonly ICurrentUserService? _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="ReactivateIndividualCommandHandler"/>.
    /// </summary>
    public ReactivateIndividualCommandHandler(
        IApplicationDbContext dbContext,
        IEventPublisher eventPublisher,
        ICurrentUserService? currentUserService = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _currentUserService = currentUserService;
    }

    /// <inheritdoc/>
    public async Task<AdminIndividualStatusResultDto> Handle(ReactivateIndividualCommand request, CancellationToken cancellationToken)
    {
        var effectiveAdminUserId = _currentUserService?.UserId ?? request.AdminUserId;
        if (string.IsNullOrWhiteSpace(effectiveAdminUserId))
        {
            throw new UnauthorizedAccessException("Authenticated admin user is required.");
        }

        var adminProfile = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == effectiveAdminUserId && !a.IsDeleted && a.IsActive, cancellationToken);

        if (adminProfile == null || (adminProfile.Role != AdminRoleType.SuperAdmin && adminProfile.Role != AdminRoleType.Admin))
        {
            throw new UnauthorizedAccessException("User is not authorized to reactivate individual profiles.");
        }

        var trimmedId = request.Id.Trim();
        var isGuid = Guid.TryParse(trimmedId, out var parsedGuid);

        var profile = await _dbContext.IndividualProfiles
            .FirstOrDefaultAsync(p => (isGuid && p.Id == parsedGuid) || p.UserId == trimmedId, cancellationToken)
            ?? throw new KeyNotFoundException($"Individual with identifier '{request.Id}' was not found.");

        profile.Reactivate();

        _dbContext.AuditLogs.Add(AuditLog.Create(
            actorId: effectiveAdminUserId,
            action: AuditActions.IndividualReactivated,
            resourceType: AuditResourceTypes.User,
            resourceId: profile.UserId,
            afterJson: JsonSerializer.Serialize(new
            {
                Reason = request.Reason,
                ProfileId = profile.Id,
                IsSuspended = profile.IsSuspended
            })));

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventPublisher.PublishAsync(
            new IndividualReactivatedDomainEvent(
                profile.Id,
                profile.UserId,
                request.Reason,
                effectiveAdminUserId,
                DateTime.UtcNow),
            cancellationToken);

        var displayStatus = profile.KycStatus == KycStatus.Verified ? "Active" : profile.KycStatus.ToString();

        return new AdminIndividualStatusResultDto(
            profile.Id,
            profile.UserId,
            displayStatus,
            profile.IsSuspended,
            profile.SuspendedAtUtc,
            profile.SuspensionReason);
    }
}
