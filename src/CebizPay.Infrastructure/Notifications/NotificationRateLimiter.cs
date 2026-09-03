using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Notifications;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Communication.Enums;
using CebizPay.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Notifications;

/// <summary>
/// Rate limiter and flood protection service for external notification channels.
/// Prevents noisy event triggers from exhausting recipient attention and third-party SMS/email provider budgets.
/// </summary>
public sealed partial class NotificationRateLimiter : INotificationRateLimiter
{
    private readonly IApplicationDbContext _dbContext;
    private readonly NotificationOptions _options;
    private readonly ILogger<NotificationRateLimiter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="NotificationRateLimiter"/>.
    /// </summary>
    public NotificationRateLimiter(
        IApplicationDbContext dbContext,
        IOptions<NotificationOptions> options,
        ILogger<NotificationRateLimiter> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _options = options?.Value ?? new NotificationOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<bool> ShouldAllowDispatchAsync(
        string recipientId,
        NotificationChannel channel,
        NotificationPriority priority,
        CancellationToken cancellationToken = default)
    {
        // 1. Critical urgency notifications (security alerts and organization suspensions) always bypass rate limits
        if (priority == NotificationPriority.Critical)
        {
            return true;
        }

        // 2. In-app notifications are internal and durable; never rate-limited
        if (channel == NotificationChannel.InApp)
        {
            return true;
        }

        var maxPerHour = channel switch
        {
            NotificationChannel.Sms => _options.MaxSmsPerHour,
            NotificationChannel.Email => _options.MaxEmailPerHour,
            NotificationChannel.Push => _options.MaxPushPerHour,
            _ => 100
        };

        var windowStart = DateTime.UtcNow.AddHours(-1);

        var recentCount = await _dbContext.NotificationDeliveryRecords
            .Where(r =>
                r.RecipientId == recipientId &&
                r.Channel == channel &&
                r.AttemptedAtUtc >= windowStart &&
                r.Status == NotificationDeliveryStatus.Delivered)
            .CountAsync(cancellationToken);

        if (recentCount >= maxPerHour)
        {
            LogRateLimitExceeded(_logger, recipientId, channel.ToString(), recentCount, maxPerHour);
            return false;
        }

        return true;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Rate limit exceeded for recipient {RecipientId} on channel {Channel}. Volume in last hour: {RecentCount}/{MaxPerHour}")]
    private static partial void LogRateLimitExceeded(ILogger logger, string recipientId, string channel, int recentCount, int maxPerHour);
}
