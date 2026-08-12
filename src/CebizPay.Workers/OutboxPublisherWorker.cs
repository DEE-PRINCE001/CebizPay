using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Workers;

/// <summary>
/// Background worker that queries unprocessed outbox messages from PostgreSQL
/// and publishes them via the messaging abstraction (RabbitMQ).
/// </summary>
public sealed partial class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherWorker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxPublisherWorker"/> class.
    /// </summary>
    public OutboxPublisherWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkerStarted(_logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogWorkerLoopError(_logger, ex);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        LogWorkerStopped(_logger);
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null && m.RetryCount < 5)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(20)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (messages.Count == 0)
        {
            return;
        }

        LogProcessingBatch(_logger, messages.Count);

        foreach (var message in messages)
        {
            try
            {
                await eventPublisher.PublishAsync(message.Content, cancellationToken).ConfigureAwait(false);
                message.ProcessedOnUtc = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message;
                LogMessageProcessingError(_logger, message.Id, message.Type, ex);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "OutboxPublisherWorker started.")]
    private static partial void LogWorkerStarted(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "OutboxPublisherWorker stopped.")]
    private static partial void LogWorkerStopped(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Processing batch of {Count} outbox messages.")]
    private static partial void LogProcessingBatch(ILogger logger, int count);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Error occurred while processing outbox message '{MessageId}' of type '{MessageType}'.")]
    private static partial void LogMessageProcessingError(ILogger logger, Guid messageId, string messageType, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Unhandled exception in OutboxPublisherWorker loop.")]
    private static partial void LogWorkerLoopError(ILogger logger, Exception exception);
}
