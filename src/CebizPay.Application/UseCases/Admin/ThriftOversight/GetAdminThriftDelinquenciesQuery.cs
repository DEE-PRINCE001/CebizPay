using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Permissions;
using CebizPay.Domain.Thrift.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.ThriftOversight;

/// <summary>
/// Query to retrieve a paginated queue of delinquent / suspended Thrift members requiring administrative oversight.
/// </summary>
public sealed record GetAdminThriftDelinquenciesQuery(
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<AdminThriftDelinquencyDto>>;

/// <summary>
/// Validator for GetAdminThriftDelinquenciesQuery.
/// </summary>
public sealed class GetAdminThriftDelinquenciesQueryValidator : AbstractValidator<GetAdminThriftDelinquenciesQuery>
{
    /// <summary>
    /// Initializes validation rules for GetAdminThriftDelinquenciesQuery.
    /// </summary>
    public GetAdminThriftDelinquenciesQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetAdminThriftDelinquenciesQuery.
/// </summary>
public sealed class GetAdminThriftDelinquenciesQueryHandler : IRequestHandler<GetAdminThriftDelinquenciesQuery, PagedResult<AdminThriftDelinquencyDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminThriftDelinquenciesQueryHandler"/>.
    /// </summary>
    public GetAdminThriftDelinquenciesQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AdminThriftDelinquencyDto>> Handle(GetAdminThriftDelinquenciesQuery request, CancellationToken cancellationToken)
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
            throw new UnauthorizedAccessException("Insufficient permissions to view Thrift delinquencies.");
        }

        // Query delinquent / suspended members directly
        var delinquentMembers = await _dbContext.ThriftMembers
            .Where(m => m.Status == ThriftMemberStatus.Suspended || m.ConsecutiveMissedCycles > 0)
            .ToListAsync(cancellationToken);

        var groupIds = delinquentMembers.Select(m => m.ThriftGroupId).Distinct().ToList();
        var groups = await _dbContext.ThriftGroups
            .Where(g => groupIds.Contains(g.Id))
            .ToListAsync(cancellationToken);

        var groupNames = groups.ToDictionary(g => g.Id, g => g.Name);

        var allDelinquents = new List<AdminThriftDelinquencyDto>();

        foreach (var member in delinquentMembers)
        {
            groupNames.TryGetValue(member.ThriftGroupId, out var groupName);
            var safeGroupName = groupName ?? "Unknown Group";

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim();
                var matchGroup = safeGroupName.Contains(search, StringComparison.OrdinalIgnoreCase);
                var matchUser = member.UserId.Contains(search, StringComparison.OrdinalIgnoreCase);
                if (!matchGroup && !matchUser)
                    continue;
            }

            allDelinquents.Add(new AdminThriftDelinquencyDto(
                member.Id,
                member.ThriftGroupId,
                safeGroupName,
                member.UserId,
                member.Status.ToString(),
                member.ConsecutiveMissedCycles,
                member.TotalContributed,
                member.TotalPayoutReceived,
                member.JoinedAtUtc,
                member.SuspendedAtUtc));
        }

        var totalCount = allDelinquents.Count;
        var pagedItems = allDelinquents
            .OrderByDescending(d => d.ConsecutiveMissedCycles)
            .ThenByDescending(d => d.SuspendedAtUtc ?? d.JoinedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PagedResult<AdminThriftDelinquencyDto>(pagedItems, totalCount, request.PageNumber, request.PageSize);
    }
}
