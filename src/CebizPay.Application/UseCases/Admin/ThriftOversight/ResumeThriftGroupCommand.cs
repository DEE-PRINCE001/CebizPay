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
/// Command for a Super Admin to resume a previously paused Thrift group.
/// </summary>
public sealed record ResumeThriftGroupCommand(
    Guid ThriftGroupId) : IRequest<bool>;

/// <summary>
/// Validator for ResumeThriftGroupCommand.
/// </summary>
public sealed class ResumeThriftGroupCommandValidator : AbstractValidator<ResumeThriftGroupCommand>
{
    /// <summary>
    /// Initializes validation rules for ResumeThriftGroupCommand.
    /// </summary>
    public ResumeThriftGroupCommandValidator()
    {
        RuleFor(x => x.ThriftGroupId)
            .NotEmpty().WithMessage("ThriftGroupId is required.");
    }
}

/// <summary>
/// Handler for ResumeThriftGroupCommand.
/// </summary>
public sealed class ResumeThriftGroupCommandHandler : IRequestHandler<ResumeThriftGroupCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="ResumeThriftGroupCommandHandler"/>.
    /// </summary>
    public ResumeThriftGroupCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<bool> Handle(ResumeThriftGroupCommand request, CancellationToken cancellationToken)
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
            throw new UnauthorizedAccessException("Only authorized Super Admins can resume Thrift groups.");
        }

        var group = await _dbContext.ThriftGroups
            .FirstOrDefaultAsync(g => g.Id == request.ThriftGroupId, cancellationToken)
            ?? throw new KeyNotFoundException($"Thrift group '{request.ThriftGroupId}' not found.");

        var now = DateTime.UtcNow;
        group.Resume(now);

        _dbContext.AuditLogs.Add(AuditLog.Create(
            actorId: callerUserId,
            action: AuditActions.ThriftGroupResumed,
            resourceType: AuditResourceTypes.ThriftGroup,
            resourceId: group.Id.ToString(),
            organizationId: group.OrganizationId,
            afterJson: JsonSerializer.Serialize(new
            {
                GroupName = group.Name,
                ResumedAtUtc = now
            })));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
