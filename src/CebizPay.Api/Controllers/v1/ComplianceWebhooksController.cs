#pragma warning disable CS1591
using System.Text;
using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Domain.Compliance.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Inbound webhook ingestion endpoints for external KYC/KYB compliance providers.
/// Authenticates cryptographic signatures, deduplicates deliveries, and processes callbacks asynchronously.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance/webhooks")]
[AllowAnonymous]
public sealed class ComplianceWebhooksController : ControllerBase
{
    private readonly IComplianceWebhookProcessor _webhookProcessor;

    public ComplianceWebhooksController(IComplianceWebhookProcessor webhookProcessor)
    {
        _webhookProcessor = webhookProcessor ?? throw new ArgumentNullException(nameof(webhookProcessor));
    }

    /// <summary>
    /// Webhook ingestion endpoint for Dojah identity and business verification callbacks.
    /// </summary>
    [HttpPost("dojah")]
    public async Task<IActionResult> DojahWebhook(CancellationToken cancellationToken)
    {
        return await ProcessWebhookInternalAsync(VerificationProvider.Dojah, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Webhook ingestion endpoint for Smile ID KYC and biometric job completion callbacks.
    /// </summary>
    [HttpPost("smile-id")]
    public async Task<IActionResult> SmileIdWebhook(CancellationToken cancellationToken)
    {
        return await ProcessWebhookInternalAsync(VerificationProvider.SmileId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Webhook ingestion endpoint for Ninja verification callbacks.
    /// </summary>
    [HttpPost("ninja")]
    public async Task<IActionResult> NinjaWebhook(CancellationToken cancellationToken)
    {
        return await ProcessWebhookInternalAsync(VerificationProvider.Ninja, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IActionResult> ProcessWebhookInternalAsync(VerificationProvider provider, CancellationToken cancellationToken)
    {
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            rawBody = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var result = await _webhookProcessor.ProcessWebhookAsync(provider, rawBody, headers, cancellationToken).ConfigureAwait(false);

        return result.Status switch
        {
            ComplianceWebhookProcessingStatus.Processed => Ok(new { status = "success", message = result.Message }),
            ComplianceWebhookProcessingStatus.Duplicate => Ok(new { status = "success", message = result.Message ?? "Duplicate event acknowledged." }),
            ComplianceWebhookProcessingStatus.Ignored => Ok(new { status = "success", message = result.Message ?? "Event ignored." }),
            ComplianceWebhookProcessingStatus.InvalidSignature => Unauthorized(new { status = "error", message = result.Message ?? "Invalid webhook signature." }),
            ComplianceWebhookProcessingStatus.InvalidPayload => BadRequest(new { status = "error", message = result.Message ?? "Invalid payload." }),
            ComplianceWebhookProcessingStatus.Error => StatusCode(500, new { status = "error", message = "Internal processing error." }),
            _ => Ok(new { status = "success" })
        };
    }
}
