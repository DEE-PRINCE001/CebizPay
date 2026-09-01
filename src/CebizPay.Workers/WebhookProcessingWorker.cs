#pragma warning disable CA1848, CA1873, CS1591
using CebizPay.Application.Common.Interfaces.Payments;

namespace CebizPay.Workers;

/// <summary>
/// Background worker continuously claiming and processing pending durable webhook events
/// across financial and compliance provider pipelines with concurrency safety and bounded retries.
/// </summary>
public sealed class WebhookProcessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WebhookProcessingWorker> _logger;

    public WebhookProcessingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<WebhookProcessingWorker> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebhookProcessingWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var processedCount = 0;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processingService = scope.ServiceProvider.GetRequiredService<IWebhookProcessingService>();

                processedCount = await processingService.ProcessPendingWebhooksBatchAsync(50, stoppingToken).ConfigureAwait(false);
                if (processedCount > 0)
                {
                    _logger.LogInformation("WebhookProcessingWorker successfully processed {Count} webhook event(s).", processedCount);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in WebhookProcessingWorker execution cycle.");
            }

            var delay = processedCount > 0 ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(10);
            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("WebhookProcessingWorker stopped.");
    }
}
