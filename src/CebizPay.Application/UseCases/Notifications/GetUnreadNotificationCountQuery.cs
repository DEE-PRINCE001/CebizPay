using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using MediatR;

namespace CebizPay.Application.UseCases.Notifications;

/// <summary>
/// Query to retrieve the count of unread in-app notifications for the authenticated user.
/// </summary>
public sealed record GetUnreadNotificationCountQuery : IRequest<int>;

/// <summary>
/// Handler for GetUnreadNotificationCountQuery.
/// </summary>
public sealed class GetUnreadNotificationCountQueryHandler : IRequestHandler<GetUnreadNotificationCountQuery, int>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetUnreadNotificationCountQueryHandler"/>.
    /// </summary>
    public GetUnreadNotificationCountQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<int> Handle(GetUnreadNotificationCountQuery request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        return await _dbContext.InAppNotifications
            .Where(n => n.UserId == callerUserId && n.ReadAtUtc == null)
            .CountAsync(cancellationToken);
    }
}
