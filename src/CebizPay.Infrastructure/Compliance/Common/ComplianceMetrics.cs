#pragma warning disable CS1591
using System.Diagnostics.Metrics;
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Infrastructure.Compliance.Common;

/// <summary>
/// OpenTelemetry and Prometheus compliance verification metrics.
/// Adheres strictly to PII protection standards: no BVN, NIN, phone, email, or customer identifiers are emitted as metric tags.
/// </summary>
public static class ComplianceMetrics
{
    private static readonly Meter Meter = new("CebizPay.Compliance", "1.0.0");

    private static readonly Counter<long> VerificationRequestsCounter =
        Meter.CreateCounter<long>("verification_requests_total", "count", "Total number of verification requests initiated");

    private static readonly Counter<long> VerificationSuccessCounter =
        Meter.CreateCounter<long>("verification_success_total", "count", "Total number of successful verification matches");

    private static readonly Counter<long> VerificationFailureCounter =
        Meter.CreateCounter<long>("verification_failure_total", "count", "Total number of failed verifications or mismatches");

    private static readonly Counter<long> VerificationPendingCounter =
        Meter.CreateCounter<long>("verification_pending_total", "count", "Total number of asynchronous pending verifications");

    private static readonly Counter<long> VerificationFallbackCounter =
        Meter.CreateCounter<long>("verification_fallback_total", "count", "Total number of verification failover engagements");

    private static readonly Counter<long> WebhookEventsCounter =
        Meter.CreateCounter<long>("provider_webhook_total", "count", "Total number of inbound compliance webhooks received");

    private static readonly Counter<long> WebhookDuplicatesCounter =
        Meter.CreateCounter<long>("provider_webhook_duplicate_total", "count", "Total number of duplicate compliance webhooks acknowledged");

    private static readonly Histogram<double> VerificationDurationHistogram =
        Meter.CreateHistogram<double>("verification_duration_ms", "ms", "Duration of external compliance verification calls in milliseconds");

    public static void RecordRequest(VerificationCapability capability, VerificationProvider provider)
    {
        VerificationRequestsCounter.Add(1,
            new KeyValuePair<string, object?>("capability", capability.ToString()),
            new KeyValuePair<string, object?>("provider", provider.ToString()));
    }

    public static void RecordSuccess(VerificationCapability capability, VerificationProvider provider, double durationMs)
    {
        VerificationSuccessCounter.Add(1,
            new KeyValuePair<string, object?>("capability", capability.ToString()),
            new KeyValuePair<string, object?>("provider", provider.ToString()));

        VerificationDurationHistogram.Record(durationMs,
            new KeyValuePair<string, object?>("capability", capability.ToString()),
            new KeyValuePair<string, object?>("provider", provider.ToString()),
            new KeyValuePair<string, object?>("result", "match"));
    }

    public static void RecordFailure(VerificationCapability capability, VerificationProvider provider, string failureType, double durationMs)
    {
        VerificationFailureCounter.Add(1,
            new KeyValuePair<string, object?>("capability", capability.ToString()),
            new KeyValuePair<string, object?>("provider", provider.ToString()),
            new KeyValuePair<string, object?>("failure_type", failureType));

        VerificationDurationHistogram.Record(durationMs,
            new KeyValuePair<string, object?>("capability", capability.ToString()),
            new KeyValuePair<string, object?>("provider", provider.ToString()),
            new KeyValuePair<string, object?>("result", "failure"));
    }

    public static void RecordPending(VerificationCapability capability, VerificationProvider provider)
    {
        VerificationPendingCounter.Add(1,
            new KeyValuePair<string, object?>("capability", capability.ToString()),
            new KeyValuePair<string, object?>("provider", provider.ToString()));
    }

    public static void RecordFallback(VerificationCapability capability, VerificationProvider primary, VerificationProvider fallback)
    {
        VerificationFallbackCounter.Add(1,
            new KeyValuePair<string, object?>("capability", capability.ToString()),
            new KeyValuePair<string, object?>("primary_provider", primary.ToString()),
            new KeyValuePair<string, object?>("fallback_provider", fallback.ToString()));
    }

    public static void RecordWebhook(VerificationProvider provider, string eventType)
    {
        WebhookEventsCounter.Add(1,
            new KeyValuePair<string, object?>("provider", provider.ToString()),
            new KeyValuePair<string, object?>("event_type", eventType));
    }

    public static void RecordWebhookDuplicate(VerificationProvider provider)
    {
        WebhookDuplicatesCounter.Add(1,
            new KeyValuePair<string, object?>("provider", provider.ToString()));
    }
}
