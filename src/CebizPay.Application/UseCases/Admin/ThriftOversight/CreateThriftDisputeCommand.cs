using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Thrift.Entities;
using CebizPay.Application.Common.Extensions;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.ThriftOversight;

/// <summary>
/// Command to lodge a new Thrift oversight dispute.
/// </summary>
public sealed record CreateThriftDisputeCommand(
    Guid ThriftGroupId,
    Guid? CycleId,
    Guid? MemberId,
    string Reason) : IRequest<ThriftDisputeDto>;

/// <summary>
/// Validator for CreateThriftDisputeCommand.
/// </summary>
public sealed class CreateThriftDisputeCommandValidator : AbstractValidator<CreateThriftDisputeCommand>
{
    /// <summary>
    /// Initializes validation rules for CreateThriftDisputeCommand.
    /// </summary>
    public CreateThriftDisputeCommandValidator()
    {
        RuleFor(x => x.ThriftGroupId)
            .NotEmpty().WithMessage("ThriftGroupId is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.");
    }
}

/// <summary>
/// Handler for CreateThriftDisputeCommand.
/// </summary>
public sealed class CreateThriftDisputeCommandHandler : IRequestHandler<CreateThriftDisputeCommand, ThriftDisputeDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="CreateThriftDisputeCommandHandler"/>.
    /// </summary>
    public CreateThriftDisputeCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<ThriftDisputeDto> Handle(CreateThriftDisputeCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var group = await _dbContext.ThriftGroups
            .FirstOrDefaultAsync(g => g.Id == request.ThriftGroupId, cancellationToken)
            ?? throw new KeyNotFoundException($"Thrift group '{request.ThriftGroupId}' not found.");

        var dispute = ThriftDispute.Create(
            request.ThriftGroupId,
            request.CycleId,
            request.MemberId,
            callerUserId,
            request.Reason);

        _dbContext.ThriftDisputes.Add(dispute);

        _dbContext.AuditLogs.Add(AuditLog.Create(
            actorId: callerUserId,
            action: AuditActions.ThriftDisputeCreated,
            resourceType: AuditResourceTypes.ThriftDispute,
            resourceId: dispute.Id.ToString(),
            organizationId: group.OrganizationId,
            afterJson: JsonSerializer.Serialize(new
            {
                ThriftGroupId = request.ThriftGroupId,
                Reason = request.Reason,
                ReportedByUserId = callerUserId
            })));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ThriftDisputeDto(
            dispute.Id,
            dispute.ThriftGroupId,
            group.Name,
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
