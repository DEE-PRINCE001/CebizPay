using CebizPay.Application.Common.Interfaces.Notifications;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Communication.Entities;
using CebizPay.Domain.Communication.Enums;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Notifications.Channels;

/// <summary>
/// In-app notification channel that persists notifications into the user's notification center table.
/// </summary>
public sealed partial class InAppNotificationChannel : IInAppNotificationChannel
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<InAppNotificationChannel> _logger;

    /// <inheritdoc/>
    public NotificationChannel Channel => NotificationChannel.InApp;

    /// <summary>
    /// Initializes a new instance of <see cref="InAppNotificationChannel"/>.
    /// </summary>
    public InAppNotificationChannel(
        IApplicationDbContext dbContext,
        ILogger<InAppNotificationChannel> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<NotificationDeliveryResult> DispatchAsync(NotificationPayload payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        try
        {
            var notification = InAppNotification.Create(
                payload.RecipientUserId,
                payload.OrganizationId,
                payload.Type,
                payload.Title,
                payload.Body,
                payload.Priority,
                payload.DeepLink,
                payload.EventId);

            _dbContext.InAppNotifications.Add(notification);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new NotificationDeliveryResult(NotificationChannel.InApp, NotificationDeliveryStatus.Delivered);
        }
        catch (Exception ex)
        {
            LogPersistError(_logger, payload.RecipientUserId, ex);
            return new NotificationDeliveryResult(NotificationChannel.InApp, NotificationDeliveryStatus.Failed, ex.Message);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to persist InAppNotification for user {UserId}")]
    private static partial void LogPersistError(ILogger logger, string userId, Exception exception);
}
