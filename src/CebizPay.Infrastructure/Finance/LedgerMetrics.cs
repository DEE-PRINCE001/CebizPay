using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CebizPay.Infrastructure.Finance;

/// <summary>
/// Centralized OpenTelemetry metrics for central ledger postings, reversals, and financial durations.
/// Emits structured metrics without leaking user IDs, balances, or sensitive account references.
/// </summary>
public static class LedgerMetrics
{
    private static readonly Meter Meter = new("CebizPay.Ledger", "1.0.0");

    private static readonly Counter<long> PostingsTotal =
        Meter.CreateCounter<long>(
            "ledger_postings_total",
            description: "Total number of atomic ledger transactions posted.");

    private static readonly Histogram<double> PostingDuration =
        Meter.CreateHistogram<double>(
            "ledger_posting_duration_ms",
            unit: "ms",
            description: "Execution duration of atomic double-entry ledger transactions in milliseconds.");

    private static readonly Counter<long> ReversalsTotal =
        Meter.CreateCounter<long>(
            "ledger_reversals_total",
            description: "Total number of financial transaction reversals executed.");

    private static readonly Counter<long> PostingFailuresTotal =
        Meter.CreateCounter<long>(
            "ledger_posting_failures_total",
            description: "Total number of failed ledger postings.");

    /// <summary>
    /// Records a ledger posting attempt and duration.
    /// </summary>
    public static void RecordPosting(string transactionType, string currency, bool succeeded, double durationMs)
    {
        var tags = new TagList
        {
            { "transaction_type", transactionType },
            { "currency", currency },
            { "result", succeeded ? "Success" : "Failed" }
        };

        PostingsTotal.Add(1, tags);
        PostingDuration.Record(durationMs, tags);

        if (!succeeded)
        {
            PostingFailuresTotal.Add(1, tags);
        }
    }

    /// <summary>
    /// Records a transaction reversal.
    /// </summary>
    public static void RecordReversal(string reason, bool succeeded)
    {
        var tags = new TagList
        {
            { "reason", reason },
            { "result", succeeded ? "Success" : "Failed" }
        };

        ReversalsTotal.Add(1, tags);
    }
}
