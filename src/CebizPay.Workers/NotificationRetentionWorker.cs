using CebizPay.Domain.Communication.Enums;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Workers;

/// <summary>
/// Background worker that periodically cleans up aged, read in-app notifications
/// according to configured retention policies. Preserves security alerts and unread records indefinitely.
/// </summary>
public sealed partial class NotificationRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationOptions _options;
    private readonly ILogger<NotificationRetentionWorker> _logger;
    private static readonly TimeSpan ExecutionInterval = TimeSpan.FromHours(24);

    /// <summary>
    /// Initializes a new instance of <see cref="NotificationRetentionWorker"/>.
    /// </summary>
    public NotificationRetentionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationOptions> options,
        ILogger<NotificationRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options?.Value ?? new NotificationOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogRetentionWorkerStarted(_logger);

        // Initial jitter delay
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeAgedNotificationsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogRetentionError(_logger, ex);
            }

            try
            {
                await Task.Delay(ExecutionInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        LogRetentionWorkerStopped(_logger);
    }

    private async Task PurgeAgedNotificationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
        var now = DateTime.UtcNow;

        // Never delete SecurityAlert notifications and never delete unread notifications
        var expiredOrAged = await db.InAppNotifications
            .Where(n =>
                n.Type != NotificationType.SecurityAlert &&
                ((n.ReadAtUtc != null && n.ReadAtUtc < cutoff) ||
                 (n.ExpiresAtUtc != null && n.ExpiresAtUtc < now)))
            .Take(500)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (expiredOrAged.Count > 0)
        {
            LogPurgingAged(_logger, expiredOrAged.Count);
            db.InAppNotifications.RemoveRange(expiredOrAged);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "NotificationRetentionWorker started.")]
    private static partial void LogRetentionWorkerStarted(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "NotificationRetentionWorker stopped.")]
    private static partial void LogRetentionWorkerStopped(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Purging {Count} aged read/expired in-app notifications.")]
    private static partial void LogPurgingAged(ILogger logger, int count);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Error occurred during notification retention cleanup execution.")]
    private static partial void LogRetentionError(ILogger logger, Exception exception);
}
