using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CebizPay.Infrastructure.Persistence.Outbox;

/// <summary>
/// Centralized OpenTelemetry metrics for the transactional outbox messaging subsystem.
/// </summary>
public static class OutboxMetrics
{
    private static readonly Meter Meter = new("CebizPay.Outbox", "1.0.0");

    private static readonly Counter<long> PublishedTotal =
        Meter.CreateCounter<long>(
            "outbox_published_total",
            description: "Total number of outbox messages successfully published to message broker.");

    private static readonly Counter<long> FailuresTotal =
        Meter.CreateCounter<long>(
            "outbox_failures_total",
            description: "Total number of transient failures while attempting to publish outbox messages.");

    private static readonly Counter<long> DeadLetteredTotal =
        Meter.CreateCounter<long>(
            "outbox_dead_lettered_total",
            description: "Total number of outbox messages marked as dead-lettered after exceeding retry limit.");

    private static readonly Histogram<double> PublishDuration =
        Meter.CreateHistogram<double>(
            "outbox_publish_duration_ms",
            unit: "ms",
            description: "Duration of outbox message publishing operations in milliseconds.");

    /// <summary>
    /// Records a published outbox message.
    /// </summary>
    public static void RecordPublished(string messageType, double durationMs)
    {
        var tags = new TagList { { "message_type", messageType } };
        PublishedTotal.Add(1, tags);
        PublishDuration.Record(durationMs, tags);
    }

    /// <summary>
    /// Records an outbox publishing transient failure.
    /// </summary>
    public static void RecordFailure(string messageType)
    {
        var tags = new TagList { { "message_type", messageType } };
        FailuresTotal.Add(1, tags);
    }

    /// <summary>
    /// Records a dead-lettered outbox message.
    /// </summary>
    public static void RecordDeadLettered(string messageType)
    {
        var tags = new TagList { { "message_type", messageType } };
        DeadLetteredTotal.Add(1, tags);
    }
}
