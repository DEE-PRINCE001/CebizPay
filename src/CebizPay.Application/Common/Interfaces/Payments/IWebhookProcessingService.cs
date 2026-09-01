#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Service coordinating asynchronous worker claiming, execution, and retries for durable webhook events.
/// </summary>
public interface IWebhookProcessingService
{
    /// <summary>
    /// Claims and processes a batch of pending webhook events across financial and compliance tables.
    /// </summary>
    Task<int> ProcessPendingWebhooksBatchAsync(int batchSize = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Directly processes or retries a specific financial WebhookEvent by ID.
    /// </summary>
    Task<WebhookProcessingResult> ProcessSingleFinancialWebhookAsync(Guid webhookEventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Directly processes or retries a specific ComplianceWebhookEvent by ID.
    /// </summary>
    Task<ComplianceWebhookProcessingResult> ProcessSingleComplianceWebhookAsync(Guid complianceWebhookEventId, CancellationToken cancellationToken = default);
}
