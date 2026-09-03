using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using MediatR;

namespace CebizPay.Application.UseCases.Notifications;

/// <summary>
/// Command to mark all unread in-app notifications for the authenticated caller as read.
/// </summary>
public sealed record MarkAllNotificationsReadCommand : IRequest<int>;

/// <summary>
/// Handler for MarkAllNotificationsReadCommand.
/// </summary>
public sealed class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand, int>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="MarkAllNotificationsReadCommandHandler"/>.
    /// </summary>
    public MarkAllNotificationsReadCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<int> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var unreadList = await _dbContext.InAppNotifications
            .Where(n => n.UserId == callerUserId && n.ReadAtUtc == null)
            .ToListAsync(cancellationToken);

        if (unreadList.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        foreach (var notification in unreadList)
        {
            notification.MarkAsRead(now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return unreadList.Count;
    }
}
