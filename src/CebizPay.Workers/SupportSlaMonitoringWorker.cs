using CebizPay.Application.UseCases.Support;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CebizPay.Workers;

/// <summary>
/// Background worker that periodically detects open support tickets that have breached their 12-hour review SLA.
/// Executes idempotently with bounded batches.
/// </summary>
public sealed partial class SupportSlaMonitoringWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SupportSlaMonitoringWorker> _logger;
    private static readonly TimeSpan ExecutionInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initializes a new instance of <see cref="SupportSlaMonitoringWorker"/>.
    /// </summary>
    public SupportSlaMonitoringWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<SupportSlaMonitoringWorker> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogSlaWorkerStarted(_logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var breachedCount = await mediator.Send(new CheckSupportSlaCommand(BatchSize: 100), stoppingToken).ConfigureAwait(false);
                if (breachedCount > 0)
                {
                    LogSlaBreachesDetected(_logger, breachedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogSlaWorkerError(_logger, ex);
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

        LogSlaWorkerStopped(_logger);
    }

    [LoggerMessage(EventId = 7001, Level = LogLevel.Information, Message = "Support SLA monitoring background worker started.")]
    private static partial void LogSlaWorkerStarted(ILogger logger);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Warning, Message = "Support SLA monitoring detected and recorded {BreachedCount} tickets breaching 12-hour review SLA.")]
    private static partial void LogSlaBreachesDetected(ILogger logger, int breachedCount);

    [LoggerMessage(EventId = 7003, Level = LogLevel.Error, Message = "Error occurred during support SLA monitoring execution.")]
    private static partial void LogSlaWorkerError(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 7004, Level = LogLevel.Information, Message = "Support SLA monitoring background worker stopped.")]
    private static partial void LogSlaWorkerStopped(ILogger logger);
}
