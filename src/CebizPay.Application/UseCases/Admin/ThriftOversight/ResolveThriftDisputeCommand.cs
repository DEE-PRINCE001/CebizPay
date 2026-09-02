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
/// Command for an authorized administrator to resolve or reject a Thrift oversight dispute.
/// </summary>
public sealed record ResolveThriftDisputeCommand(
    Guid DisputeId,
    string ResolutionNotes,
    bool Reject = false) : IRequest<ThriftDisputeDto>;

/// <summary>
/// Validator for ResolveThriftDisputeCommand.
/// </summary>
public sealed class ResolveThriftDisputeCommandValidator : AbstractValidator<ResolveThriftDisputeCommand>
{
    /// <summary>
    /// Initializes validation rules for ResolveThriftDisputeCommand.
    /// </summary>
    public ResolveThriftDisputeCommandValidator()
    {
        RuleFor(x => x.DisputeId)
            .NotEmpty().WithMessage("DisputeId is required.");

        RuleFor(x => x.ResolutionNotes)
            .NotEmpty().WithMessage("ResolutionNotes are required.");
    }
}

/// <summary>
/// Handler for ResolveThriftDisputeCommand.
/// </summary>
public sealed class ResolveThriftDisputeCommandHandler : IRequestHandler<ResolveThriftDisputeCommand, ThriftDisputeDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="ResolveThriftDisputeCommandHandler"/>.
    /// </summary>
    public ResolveThriftDisputeCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<ThriftDisputeDto> Handle(ResolveThriftDisputeCommand request, CancellationToken cancellationToken)
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
            throw new UnauthorizedAccessException("Only authorized Super Admins can resolve or reject Thrift disputes.");
        }

        var dispute = await _dbContext.ThriftDisputes
            .FirstOrDefaultAsync(d => d.Id == request.DisputeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Thrift dispute '{request.DisputeId}' not found.");

        var group = await _dbContext.ThriftGroups
            .FirstOrDefaultAsync(g => g.Id == dispute.ThriftGroupId, cancellationToken);

        var now = DateTime.UtcNow;

        if (request.Reject)
        {
            dispute.Reject(callerUserId, request.ResolutionNotes, now);
        }
        else
        {
            dispute.Resolve(callerUserId, request.ResolutionNotes, now);
        }

        var action = request.Reject ? AuditActions.ThriftDisputeRejected : AuditActions.ThriftDisputeResolved;

        _dbContext.AuditLogs.Add(AuditLog.Create(
            actorId: callerUserId,
            action: action,
            resourceType: AuditResourceTypes.ThriftDispute,
            resourceId: dispute.Id.ToString(),
            organizationId: group?.OrganizationId,
            afterJson: JsonSerializer.Serialize(new
            {
                DisputeId = dispute.Id,
                ThriftGroupId = dispute.ThriftGroupId,
                Status = dispute.Status.ToString(),
                ResolutionNotes = request.ResolutionNotes,
                ResolvedByUserId = callerUserId,
                ResolvedAtUtc = now
            })));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ThriftDisputeDto(
            dispute.Id,
            dispute.ThriftGroupId,
            group?.Name ?? "Unknown Group",
            dispute.CycleId,
            dispute.MemberId,
            dispute.ReportedByUserId,
            dispute.Reason,
            dispute.Status.ToString(),
            dispute.ResolutionNotes,
            dispute.ResolvedByUserId,
            dispute.CreatedAtUtc,
            dispute.ResolvedAtUtc);
    }
}
