using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Permissions;
using CebizPay.Domain.Thrift.Enums;
using CebizPay.Application.Common.Extensions;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.ThriftOversight;

/// <summary>
/// Query to retrieve a paginated list of Thrift oversight disputes.
/// </summary>
public sealed record GetAdminThriftDisputesQuery(
    ThriftDisputeStatus? Status = null,
    Guid? ThriftGroupId = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<ThriftDisputeDto>>;

/// <summary>
/// Validator for GetAdminThriftDisputesQuery.
/// </summary>
public sealed class GetAdminThriftDisputesQueryValidator : AbstractValidator<GetAdminThriftDisputesQuery>
{
    /// <summary>
    /// Initializes validation rules for GetAdminThriftDisputesQuery.
    /// </summary>
    public GetAdminThriftDisputesQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetAdminThriftDisputesQuery.
/// </summary>
public sealed class GetAdminThriftDisputesQueryHandler : IRequestHandler<GetAdminThriftDisputesQuery, PagedResult<ThriftDisputeDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminThriftDisputesQueryHandler"/>.
    /// </summary>
    public GetAdminThriftDisputesQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<ThriftDisputeDto>> Handle(GetAdminThriftDisputesQuery request, CancellationToken cancellationToken)
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
            throw new UnauthorizedAccessException("Insufficient permissions to view Thrift disputes.");
        }

        var query = _dbContext.ThriftDisputes.AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(d => d.Status == request.Status.Value);
        }

        if (request.ThriftGroupId.HasValue)
        {
            query = query.Where(d => d.ThriftGroupId == request.ThriftGroupId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var disputes = await query
            .OrderByDescending(d => d.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var groupIds = disputes.Select(d => d.ThriftGroupId).Distinct().ToList();
        var matchingGroups = await _dbContext.ThriftGroups
            .Where(g => groupIds.Contains(g.Id))
            .ToListAsync(cancellationToken);
        var groupNames = matchingGroups.ToDictionary(g => g.Id, g => g.Name);

        var items = disputes.Select(d =>
        {
            groupNames.TryGetValue(d.ThriftGroupId, out var groupName);
            return new ThriftDisputeDto(
                d.Id,
                d.ThriftGroupId,
                groupName ?? "Unknown Group",
                d.CycleId,
                d.MemberId,
                d.ReportedByUserId,
                d.Reason,
                d.Status.ToString(),
                d.ResolutionNotes,
                d.ResolvedByUserId,
                d.CreatedAtUtc,
                d.ResolvedAtUtc);
        }).ToList();

        return new PagedResult<ThriftDisputeDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
