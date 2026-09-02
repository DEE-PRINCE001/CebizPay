using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Interfaces.Thrift;
using CebizPay.Domain.Permissions;
using CebizPay.Domain.Thrift.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.ThriftOversight;

/// <summary>
/// Query to retrieve detailed administrative oversight data for a specific Thrift group.
/// </summary>
public sealed record GetAdminThriftGroupDetailsQuery(
    Guid ThriftGroupId) : IRequest<AdminThriftGroupDetailsDto>;

/// <summary>
/// Validator for GetAdminThriftGroupDetailsQuery.
/// </summary>
public sealed class GetAdminThriftGroupDetailsQueryValidator : AbstractValidator<GetAdminThriftGroupDetailsQuery>
{
    /// <summary>
    /// Initializes validation rules for GetAdminThriftGroupDetailsQuery.
    /// </summary>
    public GetAdminThriftGroupDetailsQueryValidator()
    {
        RuleFor(x => x.ThriftGroupId)
            .NotEmpty().WithMessage("ThriftGroupId is required.");
    }
}

/// <summary>
/// Handler for GetAdminThriftGroupDetailsQuery.
/// </summary>
public sealed class GetAdminThriftGroupDetailsQueryHandler : IRequestHandler<GetAdminThriftGroupDetailsQuery, AdminThriftGroupDetailsDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminThriftGroupDetailsQueryHandler"/>.
    /// </summary>
    public GetAdminThriftGroupDetailsQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<AdminThriftGroupDetailsDto> Handle(GetAdminThriftGroupDetailsQuery request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var callerAdmin = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive, cancellationToken);

        if (callerAdmin == null || !callerAdmin.HasPermission(Permissions.ThriftView))
        {
            throw new UnauthorizedAccessException("Insufficient permissions to view Thrift group details.");
        }

        var group = await _dbContext.ThriftGroups
            .FirstOrDefaultAsync(g => g.Id == request.ThriftGroupId, cancellationToken)
            ?? throw new KeyNotFoundException($"Thrift group '{request.ThriftGroupId}' not found.");

        string? orgName = null;
        if (group.OrganizationId.HasValue)
        {
            var org = await _dbContext.Organizations
                .FirstOrDefaultAsync(o => o.Id == group.OrganizationId.Value, cancellationToken);
            orgName = org?.CompanyName;
        }

        var members = await _dbContext.ThriftMembers
            .Where(m => m.ThriftGroupId == group.Id)
            .ToListAsync(cancellationToken);

        var cycles = await _dbContext.ThriftCycles
            .Where(c => c.ThriftGroupId == group.Id)
            .ToListAsync(cancellationToken);

        var activeCount = members.Count(m => m.Status == ThriftMemberStatus.Active);
        var totalVolume = group.TotalPositions * group.ContributionAmount;

        var summary = new AdminThriftGroupSummaryDto(
            group.Id,
            group.OrganizationId,
            orgName,
            group.CreatorUserId,
            group.Name,
            group.Description,
            group.Currency,
            group.ContributionAmount,
            group.Frequency,
            group.TotalPositions,
            activeCount,
            group.Status,
            group.CurrentCycleNumber,
            totalVolume,
            group.StartDateUtc,
            group.EndDateUtc,
            group.CreatedAtUtc);

        var memberDtos = members.Select(m => new ThriftMemberDto(
            m.Id,
            m.ThriftGroupId,
            m.UserId,
            m.Position,
            m.Status,
            m.ConsecutiveMissedCycles,
            m.TotalContributed,
            m.TotalPayoutReceived,
            m.JoinedAtUtc,
            m.PositionSelectedAtUtc,
            m.SuspendedAtUtc)).ToList();

        var cycleDtos = cycles.Select(c => new ThriftCycleDto(
            c.Id,
            c.ThriftGroupId,
            c.CycleNumber,
            c.StartDateUtc,
            c.EndDateUtc,
            c.DueDateUtc,
            c.TargetPayoutPosition,
            c.TargetBeneficiaryUserId,
            c.TotalExpectedPool,
            c.TotalCollectedPool,
            c.Status,
            c.PayoutCompletedAtUtc,
            c.PayoutLedgerTransactionId,
            c.CreatedAtUtc)).ToList();

        var disputes = await _dbContext.ThriftDisputes
            .Where(d => d.ThriftGroupId == group.Id)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var disputeDtos = disputes.Select(d => new ThriftDisputeDto(
            d.Id,
            d.ThriftGroupId,
            group.Name,
            d.CycleId,
            d.MemberId,
            d.ReportedByUserId,
            d.Reason,
            d.Status.ToString(),
            d.ResolutionNotes,
            d.ResolvedByUserId,
            d.CreatedAtUtc,
            d.ResolvedAtUtc)).ToList();

        return new AdminThriftGroupDetailsDto(summary, memberDtos, cycleDtos, disputeDtos);
    }
}
