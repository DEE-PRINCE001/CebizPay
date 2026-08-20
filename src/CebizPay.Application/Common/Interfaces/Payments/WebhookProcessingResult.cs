namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Status of a webhook processing attempt.
/// </summary>
public enum WebhookProcessingStatus
{
    /// <summary>Webhook successfully verified and reconciled.</summary>
    Processed = 1,

    /// <summary>Duplicate webhook safely acknowledged without financial mutation.</summary>
    Duplicate = 2,

    /// <summary>Invalid signature or authentication header.</summary>
    InvalidSignature = 3,

    /// <summary>Invalid or malformed JSON payload structure.</summary>
    InvalidPayload = 4,

    /// <summary>Webhook safely ignored (unhandled event type or out-of-order stale update).</summary>
    Ignored = 5,

    /// <summary>Internal processing error or temporary failure.</summary>
    Error = 6
}

/// <summary>
/// Represents the result of an external payment provider webhook processing pipeline.
/// </summary>
public sealed record WebhookProcessingResult(
    WebhookProcessingStatus Status,
    string? ProviderEventId,
    string? Message,
    Guid? PaymentAttemptId = null)
{
    /// <summary>Creates a successful processed result.</summary>
    public static WebhookProcessingResult Processed(string providerEventId, Guid? paymentAttemptId = null, string? message = null) =>
        new(WebhookProcessingStatus.Processed, providerEventId, message ?? "Webhook processed successfully.", paymentAttemptId);

    /// <summary>Creates a duplicate result acknowledged without state mutation.</summary>
    public static WebhookProcessingResult Duplicate(string providerEventId, string? message = null) =>
        new(WebhookProcessingStatus.Duplicate, providerEventId, message ?? "Duplicate webhook event safely acknowledged.");

    /// <summary>Creates an invalid signature failure result.</summary>
    public static WebhookProcessingResult InvalidSignature(string? message = null) =>
        new(WebhookProcessingStatus.InvalidSignature, null, message ?? "Invalid webhook signature or verification header.");

    /// <summary>Creates an invalid payload failure result.</summary>
    public static WebhookProcessingResult InvalidPayload(string message) =>
        new(WebhookProcessingStatus.InvalidPayload, null, message);

    /// <summary>Creates an ignored result (e.g. unhandled event type).</summary>
    public static WebhookProcessingResult Ignored(string providerEventId, string reason) =>
        new(WebhookProcessingStatus.Ignored, providerEventId, reason);

    /// <summary>Creates an error result.</summary>
    public static WebhookProcessingResult Error(string providerEventId, string errorMessage) =>
        new(WebhookProcessingStatus.Error, providerEventId, errorMessage);
}
