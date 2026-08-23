using CebizPay.Application.Common.Interfaces.Savings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CebizPay.Workers;

/// <summary>
/// Background worker that executes daily interest accrual across active savings accounts without blocking HTTP request threads.
/// </summary>
public sealed partial class SavingsAccrualWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SavingsAccrualWorker> _logger;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Initializes a new instance of the <see cref="SavingsAccrualWorker"/> class.
    /// </summary>
    public SavingsAccrualWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<SavingsAccrualWorker> logger)
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
                await ProcessAccrualsAsync(stoppingToken).ConfigureAwait(false);
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

    private async Task ProcessAccrualsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var savingsService = scope.ServiceProvider.GetRequiredService<ISavingsService>();

        var nowUtc = DateTime.UtcNow;
        var processedCount = await savingsService.ProcessDailyInterestAccrualAsync(nowUtc, cancellationToken).ConfigureAwait(false);

        if (processedCount > 0)
        {
            LogAccrualsProcessed(_logger, processedCount);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "SavingsAccrualWorker started.")]
    private static partial void LogWorkerStarted(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "SavingsAccrualWorker stopped.")]
    private static partial void LogWorkerStopped(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Unhandled error in SavingsAccrualWorker execution cycle.")]
    private static partial void LogWorkerLoopError(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Processed daily interest accrual for {Count} savings accounts.")]
    private static partial void LogAccrualsProcessed(ILogger logger, int count);
}
