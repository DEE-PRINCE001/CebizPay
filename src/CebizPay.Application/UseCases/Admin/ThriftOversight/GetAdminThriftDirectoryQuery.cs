using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Permissions;
using CebizPay.Domain.Thrift.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.ThriftOversight;

/// <summary>
/// Query to retrieve a paginated directory of all platform Thrift groups for administrative oversight.
/// </summary>
public sealed record GetAdminThriftDirectoryQuery(
    ThriftStatus? Status = null,
    ThriftFrequency? Frequency = null,
    Currency? Currency = null,
    Guid? OrganizationId = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<AdminThriftGroupSummaryDto>>;

/// <summary>
/// Validator for GetAdminThriftDirectoryQuery.
/// </summary>
public sealed class GetAdminThriftDirectoryQueryValidator : AbstractValidator<GetAdminThriftDirectoryQuery>
{
    /// <summary>
    /// Initializes validation rules for GetAdminThriftDirectoryQuery.
    /// </summary>
    public GetAdminThriftDirectoryQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetAdminThriftDirectoryQuery.
/// </summary>
public sealed class GetAdminThriftDirectoryQueryHandler : IRequestHandler<GetAdminThriftDirectoryQuery, PagedResult<AdminThriftGroupSummaryDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminThriftDirectoryQueryHandler"/>.
    /// </summary>
    public GetAdminThriftDirectoryQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AdminThriftGroupSummaryDto>> Handle(GetAdminThriftDirectoryQuery request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        // Authorize caller has Thrift.View permission
        var callerAdmin = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive, cancellationToken);

        if (callerAdmin == null || !callerAdmin.HasPermission(Permissions.ThriftView))
        {
            throw new UnauthorizedAccessException("Insufficient permissions to view the administrative Thrift directory.");
        }

        var query = _dbContext.ThriftGroups.AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(g => g.Status == request.Status.Value);
        }

        if (request.Frequency.HasValue)
        {
            query = query.Where(g => g.Frequency == request.Frequency.Value);
        }

        if (request.Currency.HasValue)
        {
            query = query.Where(g => g.Currency == request.Currency.Value);
        }

        if (request.OrganizationId.HasValue)
        {
            query = query.Where(g => g.OrganizationId == request.OrganizationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(g => g.Name.Contains(search) || g.CreatorUserId.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var groups = await query
            .OrderByDescending(g => g.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var groupIds = groups.Select(g => g.Id).ToList();
        var allMembers = await _dbContext.ThriftMembers
            .Where(m => groupIds.Contains(m.ThriftGroupId))
            .ToListAsync(cancellationToken);

        var membersByGroup = allMembers
            .GroupBy(m => m.ThriftGroupId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Fetch organization names for workplace groups
        var orgIds = groups.Where(g => g.OrganizationId.HasValue).Select(g => g.OrganizationId!.Value).Distinct().ToList();
        var orgList = await _dbContext.Organizations
            .Where(o => orgIds.Contains(o.Id))
            .ToListAsync(cancellationToken);

        var orgMap = orgList.ToDictionary(o => o.Id, o => o.CompanyName);

        var items = groups.Select(g =>
        {
            string? orgName = null;
            if (g.OrganizationId.HasValue && orgMap.TryGetValue(g.OrganizationId.Value, out var name))
            {
                orgName = name;
            }

            var groupMemberList = membersByGroup.TryGetValue(g.Id, out var mList) ? mList : new List<Domain.Thrift.Entities.ThriftMember>();
            var activeCount = groupMemberList.Count(m => m.Status == ThriftMemberStatus.Active);
            var totalVolume = g.TotalPositions * g.ContributionAmount;

            return new AdminThriftGroupSummaryDto(
                g.Id,
                g.OrganizationId,
                orgName,
                g.CreatorUserId,
                g.Name,
                g.Description,
                g.Currency,
                g.ContributionAmount,
                g.Frequency,
                g.TotalPositions,
                activeCount,
                g.Status,
                g.CurrentCycleNumber,
                totalVolume,
                g.StartDateUtc,
                g.EndDateUtc,
                g.CreatedAtUtc);
        }).ToList();

        return new PagedResult<AdminThriftGroupSummaryDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
