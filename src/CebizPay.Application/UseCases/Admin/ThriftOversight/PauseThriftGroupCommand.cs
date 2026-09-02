using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using CebizPay.Application.Common.Extensions;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.ThriftOversight;

/// <summary>
/// Command for a Super Admin to pause a Thrift group for investigation or intervention.
/// </summary>
public sealed record PauseThriftGroupCommand(
    Guid ThriftGroupId,
    string Reason) : IRequest<bool>;

/// <summary>
/// Validator for PauseThriftGroupCommand.
/// </summary>
public sealed class PauseThriftGroupCommandValidator : AbstractValidator<PauseThriftGroupCommand>
{
    /// <summary>
    /// Initializes validation rules for PauseThriftGroupCommand.
    /// </summary>
    public PauseThriftGroupCommandValidator()
    {
        RuleFor(x => x.ThriftGroupId)
            .NotEmpty().WithMessage("ThriftGroupId is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.");
    }
}

/// <summary>
/// Handler for PauseThriftGroupCommand.
/// </summary>
public sealed class PauseThriftGroupCommandHandler : IRequestHandler<PauseThriftGroupCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="PauseThriftGroupCommandHandler"/>.
    /// </summary>
    public PauseThriftGroupCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<bool> Handle(PauseThriftGroupCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var callerAdmin = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive, cancellationToken);

        if (callerAdmin == null || callerAdmin.Role == AdminRoleType.Auditor || (!callerAdmin.HasPermission(Permissions.ThriftManage) && callerAdmin.Role != AdminRoleType.SuperAdmin))
        {
            throw new UnauthorizedAccessException("Only authorized Super Admins can pause Thrift groups.");
        }

        var group = await _dbContext.ThriftGroups
            .FirstOrDefaultAsync(g => g.Id == request.ThriftGroupId, cancellationToken)
            ?? throw new KeyNotFoundException($"Thrift group '{request.ThriftGroupId}' not found.");

        var now = DateTime.UtcNow;
        group.Pause(request.Reason, now);

        _dbContext.AuditLogs.Add(AuditLog.Create(
            actorId: callerUserId,
            action: AuditActions.ThriftGroupPaused,
            resourceType: AuditResourceTypes.ThriftGroup,
            resourceId: group.Id.ToString(),
            organizationId: group.OrganizationId,
            afterJson: JsonSerializer.Serialize(new
            {
                GroupName = group.Name,
                Reason = request.Reason,
                PausedAtUtc = now
            })));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
