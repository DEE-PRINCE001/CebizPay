namespace CebizPay.Workers;

/// <summary>
/// Background worker responsible for executing scheduled background tasks.
/// </summary>
public sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    /// <summary>
    /// Executes the background worker until cancellation is requested.
    /// </summary>
    /// <param name="stoppingToken">
    /// Token used to signal that the application is shutting down.
    /// </param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            LogWorkerRunning(logger, DateTimeOffset.Now, null);

            await Task.Delay(1000, stoppingToken);
        }
    }

    private static readonly Action<ILogger, DateTimeOffset, Exception?> LogWorkerRunning =
        LoggerMessage.Define<DateTimeOffset>(
            LogLevel.Information,
            new EventId(1, nameof(LogWorkerRunning)),
            "Worker running at: {Time}");
}