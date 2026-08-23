using CebizPay.Application.Common.Interfaces.Thrift;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CebizPay.Workers;

/// <summary>
/// Background worker processing 02:00 UTC automated thrift cycle collections, delinquency tracking, and pool payouts.
/// </summary>
public sealed partial class ThriftCycleWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ThriftCycleWorker> _logger;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Initializes a new instance of the <see cref="ThriftCycleWorker"/> class.
    /// </summary>
    public ThriftCycleWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ThriftCycleWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
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
                await ProcessCyclesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogWorkerLoopError(_logger, ex);
            }

            try
            {
                await Task.Delay(PollingInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        LogWorkerStopped(_logger);
    }

    private async Task ProcessCyclesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var collectionService = scope.ServiceProvider.GetRequiredService<IThriftCollectionService>();
        var payoutService = scope.ServiceProvider.GetRequiredService<IThriftPayoutService>();

        var nowUtc = DateTime.UtcNow;

        // 1. Process due collections
        var collectionsCount = await collectionService.ProcessDueCollectionsAsync(nowUtc, cancellationToken).ConfigureAwait(false);
        if (collectionsCount > 0)
        {
            LogCollectionsProcessed(_logger, collectionsCount);
        }

        // 2. Process ready payouts
        var payoutsCount = await payoutService.ProcessReadyPayoutsAsync(nowUtc, cancellationToken).ConfigureAwait(false);
        if (payoutsCount > 0)
        {
            LogPayoutsProcessed(_logger, payoutsCount);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "ThriftCycleWorker started.")]
    private static partial void LogWorkerStarted(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "ThriftCycleWorker stopped.")]
    private static partial void LogWorkerStopped(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Unhandled error in ThriftCycleWorker execution cycle.")]
    private static partial void LogWorkerLoopError(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Processed {Count} thrift cycle collections.")]
    private static partial void LogCollectionsProcessed(ILogger logger, int count);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Processed {Count} thrift cycle payouts.")]
    private static partial void LogPayoutsProcessed(ILogger logger, int count);
}
