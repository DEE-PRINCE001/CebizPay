using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CebizPay.Infrastructure.Finance;

/// <summary>
/// Centralized OpenTelemetry metrics for asynchronous background worker subsystems.
/// </summary>
public static class WorkerMetrics
{
    private static readonly Meter Meter = new("CebizPay.Workers", "1.0.0");

    private static readonly Counter<long> ExecutionsTotal =
        Meter.CreateCounter<long>(
            "worker_executions_total",
            description: "Total number of background worker execution cycles.");

    private static readonly Counter<long> ItemsProcessedTotal =
        Meter.CreateCounter<long>(
            "worker_items_processed_total",
            description: "Total number of items/jobs processed by background workers.");

    private static readonly Histogram<double> ExecutionDuration =
        Meter.CreateHistogram<double>(
            "worker_execution_duration_ms",
            unit: "ms",
            description: "Duration of background worker execution cycles in milliseconds.");

    private static readonly Counter<long> WorkerErrorsTotal =
        Meter.CreateCounter<long>(
            "worker_errors_total",
            description: "Total number of unhandled exceptions or failures during worker execution.");

    /// <summary>
    /// Records a background worker execution cycle.
    /// </summary>
    public static void RecordCycle(string workerName, int itemsProcessed, bool succeeded, double durationMs)
    {
        var tags = new TagList
        {
            { "worker_name", workerName },
            { "result", succeeded ? "Success" : "Failed" }
        };

        ExecutionsTotal.Add(1, tags);
        ExecutionDuration.Record(durationMs, tags);

        if (itemsProcessed > 0)
        {
            ItemsProcessedTotal.Add(itemsProcessed, new TagList { { "worker_name", workerName } });
        }

        if (!succeeded)
        {
            WorkerErrorsTotal.Add(1, tags);
        }
    }
}
