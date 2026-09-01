#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Status outcome of processing an inbound compliance webhook payload.
/// </summary>
public enum ComplianceWebhookProcessingStatus
{
    /// <summary>Webhook successfully authenticated, deduplicated, and processed.</summary>
    Processed = 1,

    /// <summary>Duplicate webhook delivery detected and safely acknowledged.</summary>
    Duplicate = 2,

    /// <summary>Webhook signature or verification token failed authentication.</summary>
    InvalidSignature = 3,

    /// <summary>Webhook payload was malformed or missing required correlation identifiers.</summary>
    InvalidPayload = 4,

    /// <summary>Webhook was syntactically valid but safely ignored (e.g. unhandled event type).</summary>
    Ignored = 5,

    /// <summary>Unexpected internal processing error during webhook execution.</summary>
    Error = 6
}

/// <summary>
/// Result DTO returned by <see cref="IComplianceWebhookProcessor"/>.
/// </summary>
public sealed record ComplianceWebhookProcessingResult(
    ComplianceWebhookProcessingStatus Status,
    string? ProviderEventId = null,
    string? Message = null,
    Guid? VerificationOperationId = null)
{
    public static ComplianceWebhookProcessingResult Processed(string? providerEventId, string? message = null, Guid? operationId = null) =>
        new(ComplianceWebhookProcessingStatus.Processed, providerEventId, message ?? "Compliance webhook processed successfully.", operationId);

    public static ComplianceWebhookProcessingResult Duplicate(string? providerEventId, string? message = null) =>
        new(ComplianceWebhookProcessingStatus.Duplicate, providerEventId, message ?? "Duplicate webhook event safely acknowledged.");

    public static ComplianceWebhookProcessingResult InvalidSignature(string? message = null) =>
        new(ComplianceWebhookProcessingStatus.InvalidSignature, null, message ?? "Invalid webhook signature or verification header.");

    public static ComplianceWebhookProcessingResult InvalidPayload(string? message = null) =>
        new(ComplianceWebhookProcessingStatus.InvalidPayload, null, message);

    public static ComplianceWebhookProcessingResult Ignored(string? providerEventId, string? reason = null) =>
        new(ComplianceWebhookProcessingStatus.Ignored, providerEventId, reason);

    public static ComplianceWebhookProcessingResult Error(string? providerEventId, string? errorMessage = null) =>
        new(ComplianceWebhookProcessingStatus.Error, providerEventId, errorMessage);
}
