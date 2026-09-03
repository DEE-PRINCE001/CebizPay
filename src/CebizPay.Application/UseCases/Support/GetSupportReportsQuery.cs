using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Support.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Support;

/// <summary>
/// Query to aggregate platform customer support analytics.
/// Authorized for SuperAdmin and Auditor.
/// </summary>
public sealed record GetSupportReportsQuery(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null) : IRequest<SupportReportsDto>;

/// <summary>
/// Handler for GetSupportReportsQuery.
/// </summary>
public sealed class GetSupportReportsQueryHandler : IRequestHandler<GetSupportReportsQuery, SupportReportsDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetSupportReportsQueryHandler"/>.
    /// </summary>
    public GetSupportReportsQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<SupportReportsDto> Handle(GetSupportReportsQuery request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var adminProfile = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == callerUserId && !a.IsDeleted && a.IsActive, cancellationToken);

        if (adminProfile == null || (adminProfile.Role != AdminRoleType.SuperAdmin && adminProfile.Role != AdminRoleType.Auditor))
        {
            throw new UnauthorizedAccessException("Administrative support authorization required.");
        }

        var from = request.FromUtc ?? DateTime.UtcNow.AddDays(-30);
        var to = request.ToUtc ?? DateTime.UtcNow;

        var baseQuery = _dbContext.SupportTickets
            .Where(t => t.CreatedAtUtc >= from && t.CreatedAtUtc <= to);

        var total = await baseQuery.CountAsync(cancellationToken);
        var open = await baseQuery.CountAsync(t => t.Status == SupportTicketStatus.Open, cancellationToken);
        var escalated = await baseQuery.CountAsync(t => t.Status == SupportTicketStatus.Escalated, cancellationToken);
        var inReview = await baseQuery.CountAsync(t => t.Status == SupportTicketStatus.InReview, cancellationToken);
        var resolved = await baseQuery.CountAsync(t => t.Status == SupportTicketStatus.Resolved, cancellationToken);
        var closed = await baseQuery.CountAsync(t => t.Status == SupportTicketStatus.Closed, cancellationToken);
        var slaBreached = await baseQuery.CountAsync(t => t.IsSlaBreached, cancellationToken);

        var byCategoryList = await baseQuery
            .GroupBy(t => t.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byPriorityList = await baseQuery
            .GroupBy(t => t.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byCategory = byCategoryList.ToDictionary(x => x.Category.ToString(), x => x.Count);
        var byPriority = byPriorityList.ToDictionary(x => x.Priority.ToString(), x => x.Count);

        return new SupportReportsDto(
            TotalTickets: total,
            OpenTickets: open,
            EscalatedTickets: escalated,
            InReviewTickets: inReview,
            ResolvedTickets: resolved,
            ClosedTickets: closed,
            SlaBreachedTickets: slaBreached,
            TicketsByCategory: byCategory,
            TicketsByPriority: byPriority,
            FromUtc: from,
            ToUtc: to);
    }
}
