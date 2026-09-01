#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Normalized outcome category of an external provider webhook notification.
/// </summary>
public enum NormalizedWebhookOutcome
{
    /// <summary>Definitive success reported by provider.</summary>
    Success,

    /// <summary>Definitive failure or rejection reported by provider.</summary>
    Failure,

    /// <summary>Transaction currently in progress or awaiting clearing.</summary>
    Pending,

    /// <summary>Transaction reversed or refunded by provider.</summary>
    Reversed,

    /// <summary>Provider outcome is unknown or ambiguous; requires status reconciliation query.</summary>
    Unknown
}

/// <summary>
/// Provider-neutral normalized representation of an inbound payment or funding webhook event.
/// </summary>
public sealed record NormalizedWebhookEvent(
    PaymentProvider Provider,
    string EventType,
    string ProviderEventId,
    string? InternalReference,
    string? ProviderReference,
    string? AccountNumber,
    decimal? Amount,
    string? Currency,
    NormalizedWebhookOutcome Outcome,
    string? FailureCode,
    string? FailureReason,
    DateTime? OccurredAtUtc,
    string? SafeMetadata)
{
    public bool IsSuccess => Outcome == NormalizedWebhookOutcome.Success;
    public bool IsFailure => Outcome == NormalizedWebhookOutcome.Failure;
    public bool IsReversed => Outcome == NormalizedWebhookOutcome.Reversed;
    public bool IsPending => Outcome == NormalizedWebhookOutcome.Pending;
    public bool IsUnknown => Outcome == NormalizedWebhookOutcome.Unknown;
}

/// <summary>
/// Provider-neutral normalized representation of an inbound compliance verification webhook callback.
/// </summary>
public sealed record NormalizedComplianceWebhookEvent(
    VerificationProvider Provider,
    string EventType,
    string ProviderEventId,
    string? VerificationReference,
    string? ProviderReference,
    VerificationResultStatus ResultStatus,
    double? ConfidenceScore,
    string? FailureReason,
    string? SafeMetadata);

/// <summary>
/// Contract for normalizing provider-specific webhook payloads into unified neutral models.
/// </summary>
public interface IWebhookEventNormalizer
{
    /// <summary>Normalizes financial provider webhooks (Monnify, Flutterwave, Paystack).</summary>
    NormalizedWebhookEvent? NormalizeFinancial(PaymentProvider provider, string rawPayload, IReadOnlyDictionary<string, string> headers);

    /// <summary>Normalizes compliance verification webhooks (Dojah, Smile ID, Ninja).</summary>
    NormalizedComplianceWebhookEvent? NormalizeCompliance(VerificationProvider provider, string rawPayload, IReadOnlyDictionary<string, string> headers);
}
