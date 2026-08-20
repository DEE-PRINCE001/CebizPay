using System.Text;
using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Payments.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// External payment provider webhook endpoints.
/// Ingests, authenticates, and reconciles external payment status notifications from Flutterwave and Paystack.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments/webhooks")]
[AllowAnonymous]
public sealed class PaymentsWebhookController : ControllerBase
{
    private readonly IWebhookProcessor _webhookProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentsWebhookController"/> class.
    /// </summary>
    public PaymentsWebhookController(IWebhookProcessor webhookProcessor)
    {
        _webhookProcessor = webhookProcessor ?? throw new ArgumentNullException(nameof(webhookProcessor));
    }

    /// <summary>
    /// Webhook ingestion endpoint for Flutterwave payment notifications.
    /// </summary>
    [HttpPost("flutterwave")]
    public async Task<IActionResult> FlutterwaveWebhook(CancellationToken cancellationToken)
    {
        return await ProcessWebhookInternalAsync(PaymentProvider.Flutterwave, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Webhook ingestion endpoint for Paystack payment notifications.
    /// </summary>
    [HttpPost("paystack")]
    public async Task<IActionResult> PaystackWebhook(CancellationToken cancellationToken)
    {
        return await ProcessWebhookInternalAsync(PaymentProvider.Paystack, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IActionResult> ProcessWebhookInternalAsync(PaymentProvider provider, CancellationToken cancellationToken)
    {
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            rawBody = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        var headerDictionary = Request.Headers.ToDictionary(
            h => h.Key,
            h => h.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        var result = await _webhookProcessor.ProcessWebhookAsync(
            provider: provider,
            rawPayload: rawBody,
            headers: headerDictionary,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.Status switch
        {
            WebhookProcessingStatus.Processed => Ok(new { status = "success", message = result.Message }),
            WebhookProcessingStatus.Duplicate => Ok(new { status = "success", message = result.Message ?? "Duplicate event acknowledged." }),
            WebhookProcessingStatus.Ignored => Ok(new { status = "success", message = result.Message ?? "Event ignored." }),
            WebhookProcessingStatus.InvalidSignature => Unauthorized(new { status = "error", message = result.Message ?? "Invalid webhook signature." }),
            WebhookProcessingStatus.InvalidPayload => BadRequest(new { status = "error", message = result.Message ?? "Invalid payload." }),
            WebhookProcessingStatus.Error => StatusCode(500, new { status = "error", message = "Internal processing error." }),
            _ => Ok(new { status = "success" })
        };
    }
}
