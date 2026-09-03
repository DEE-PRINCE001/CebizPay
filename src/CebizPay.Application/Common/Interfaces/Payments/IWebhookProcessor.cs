using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Ingestion and reconciliation coordinator for external payment provider webhooks.
/// </summary>
public interface IWebhookProcessor
{
    /// <summary>
    /// Authenticates, deduplicates, verifies, and reconciles an incoming payment provider webhook.
    /// </summary>
    /// <param name="provider">The payment provider sending the webhook.</param>
    /// <param name="rawPayload">The raw JSON request body.</param>
    /// <param name="headers">HTTP request headers containing verification signatures.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="WebhookProcessingResult"/> describing the processing outcome.</returns>
    Task<WebhookProcessingResult> ProcessWebhookAsync(
        PaymentProvider provider,
        string rawPayload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Authenticates, deduplicates, and durably persists an incoming payment provider webhook
    /// in Received status for asynchronous worker processing without holding financial locks.
    /// </summary>
    Task<WebhookProcessingResult> IngestWebhookAsync(
        PaymentProvider provider,
        string rawPayload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the financial reconciliation, ledger posting, and state transitions for a claimed durable webhook event.
    /// </summary>
    Task<WebhookProcessingResult> ProcessFinancialWebhookEventAsync(
        Guid webhookEventId,
        CancellationToken cancellationToken = default);
}
