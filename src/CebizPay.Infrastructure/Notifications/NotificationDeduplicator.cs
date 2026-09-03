using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Notifications;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Communication.Entities;
using CebizPay.Domain.Communication.Enums;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Notifications;

/// <summary>
/// Authoritative PostgreSQL notification deduplication ledger.
/// Guarantees application-level channel dispatch deduplication per event and recipient.
/// </summary>
public sealed partial class NotificationDeduplicator : INotificationDeduplicator
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<NotificationDeduplicator> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="NotificationDeduplicator"/>.
    /// </summary>
    public NotificationDeduplicator(
        IApplicationDbContext dbContext,
        ILogger<NotificationDeduplicator> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<bool> ShouldDispatchAsync(
        string eventId,
        string recipientId,
        NotificationType type,
        NotificationChannel channel,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.NotificationDeliveryRecords
            .FirstOrDefaultAsync(r =>
                r.EventId == eventId &&
                r.RecipientId == recipientId &&
                r.Type == type &&
                r.Channel == channel,
                cancellationToken);

        if (existing == null)
        {
            return true;
        }

        // If previously delivered, duplicate dispatch must be suppressed
        if (existing.Status == NotificationDeliveryStatus.Delivered)
        {
            return false;
        }

        // If previously throttled or failed, allow retry
        return existing.Status == NotificationDeliveryStatus.Failed;
    }

    /// <inheritdoc/>
    public async Task RecordOutcomeAsync(
        string eventId,
        string recipientId,
        NotificationType type,
        NotificationChannel channel,
        NotificationDeliveryStatus status,
        string? failureReason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _dbContext.NotificationDeliveryRecords
                .FirstOrDefaultAsync(r =>
                    r.EventId == eventId &&
                    r.RecipientId == recipientId &&
                    r.Type == type &&
                    r.Channel == channel,
                    cancellationToken);

            if (existing != null)
            {
                existing.UpdateStatus(status, failureReason);
            }
            else
            {
                var record = NotificationDeliveryRecord.Create(
                    eventId,
                    recipientId,
                    type,
                    channel,
                    status,
                    failureReason);

                _dbContext.NotificationDeliveryRecords.Add(record);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogRecordingConflict(_logger, eventId, ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Concurrency or constraint conflict while recording notification delivery outcome for {EventId}")]
    private static partial void LogRecordingConflict(ILogger logger, string eventId, Exception exception);
}
