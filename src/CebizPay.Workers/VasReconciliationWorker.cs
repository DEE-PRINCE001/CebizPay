using CebizPay.Application.Common.Interfaces.Vas;

namespace CebizPay.Workers;

/// <summary>
/// Background worker that queries in-flight, unknown, or timed-out VAS transactions and reconciles
/// their definitive fulfillment status with external provider gateways (VTUGATE).
/// </summary>
public sealed partial class VasReconciliationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VasReconciliationWorker> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="VasReconciliationWorker"/>.
    /// </summary>
    public VasReconciliationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<VasReconciliationWorker> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkerStarted(_logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            int resolvedCount = 0;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var reconciliationService = scope.ServiceProvider.GetRequiredService<IVasReconciliationService>();

                resolvedCount = await reconciliationService.ReconcileUnresolvedVasTransactionsAsync(50, stoppingToken).ConfigureAwait(false);
                if (resolvedCount > 0)
                {
                    LogBatchResolved(_logger, resolvedCount);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogWorkerLoopError(_logger, ex);
            }

            var delay = resolvedCount > 0 ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(30);
            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
        }

        LogWorkerStopped(_logger);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "VasReconciliationWorker started.")]
    private static partial void LogWorkerStarted(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "VasReconciliationWorker stopped.")]
    private static partial void LogWorkerStopped(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Error occurred in VasReconciliationWorker execution loop.")]
    private static partial void LogWorkerLoopError(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "VasReconciliationWorker successfully resolved {ResolvedCount} in-flight VAS transaction(s).")]
    private static partial void LogBatchResolved(ILogger logger, int resolvedCount);
}
