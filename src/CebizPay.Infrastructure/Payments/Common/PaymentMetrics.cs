using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CebizPay.Infrastructure.Payments.Common;

/// <summary>
/// Centralized OpenTelemetry metrics for external payment provider operations.
/// Emits structured metrics without leaking sensitive credentials, account numbers, or user emails.
/// </summary>
public static class PaymentMetrics
{
    private static readonly Meter Meter = new("CebizPay.Payments", "1.0.0");

    private static readonly Counter<long> ProviderRequestsTotal =
        Meter.CreateCounter<long>(
            "payment_provider_requests_total",
            description: "Total number of HTTP requests dispatched to external payment providers.");

    private static readonly Histogram<double> ProviderRequestDuration =
        Meter.CreateHistogram<double>(
            "payment_provider_request_duration",
            unit: "ms",
            description: "Execution duration of payment provider requests in milliseconds.");

    private static readonly Counter<long> ProviderFailuresTotal =
        Meter.CreateCounter<long>(
            "payment_provider_failures_total",
            description: "Total number of business and technical failures encountered during payment provider execution.");

    private static readonly Counter<long> ProviderUnknownTotal =
        Meter.CreateCounter<long>(
            "payment_provider_unknown_total",
            description: "Total number of indeterminate / timeout outcomes encountered during payment provider execution.");

    private static readonly Counter<long> AccountResolutionTotal =
        Meter.CreateCounter<long>(
            "payment_account_resolution_total",
            description: "Total number of destination bank account resolution attempts.");

    /// <summary>
    /// Records a payment provider request attempt and duration.
    /// </summary>
    public static void RecordRequest(string provider, string operation, string result, double durationMs)
    {
        var tags = new TagList
        {
            { "provider", provider },
            { "operation", operation },
            { "result", result }
        };

        ProviderRequestsTotal.Add(1, tags);
        ProviderRequestDuration.Record(durationMs, tags);

        if (result == "BusinessFailure" || result == "TechnicalFailure")
        {
            ProviderFailuresTotal.Add(1, tags);
        }
        else if (result == "Unknown")
        {
            ProviderUnknownTotal.Add(1, tags);
        }
    }

    /// <summary>
    /// Records a destination bank account resolution attempt.
    /// </summary>
    public static void RecordAccountResolution(string provider, bool succeeded)
    {
        var tags = new TagList
        {
            { "provider", provider },
            { "result", succeeded ? "Success" : "Failed" }
        };

        AccountResolutionTotal.Add(1, tags);
    }
}
