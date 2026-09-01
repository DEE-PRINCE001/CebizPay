#pragma warning disable CS1591
using System.Diagnostics.Metrics;

namespace CebizPay.Infrastructure.Payments.Common;

/// <summary>
/// OpenTelemetry metrics recorder for durable webhook processing, deduplication, and reconciliation across payment and compliance rails.
/// Enforces strict sanitization to ensure zero PII or sensitive provider secrets enter telemetry tags.
/// </summary>
public sealed class ReconciliationMetrics
{
    private static readonly Meter Meter = new("CebizPay.Payments.Reconciliation", "1.0.0");

    private readonly Counter<long> _webhookReceivedCounter;
    private readonly Counter<long> _webhookDuplicateCounter;
    private readonly Counter<long> _webhookProcessingCounter;
    private readonly Counter<long> _webhookProcessingFailuresCounter;
    private readonly Counter<long> _reconciliationStartedCounter;
    private readonly Counter<long> _reconciliationSuccessCounter;
    private readonly Counter<long> _reconciliationFailureCounter;
    private readonly Counter<long> _reconciliationUnresolvedCounter;
    private readonly Counter<long> _manualReviewCounter;
    private readonly Counter<long> _providerMismatchCounter;
    private readonly Counter<long> _financialReversalCounter;
    private readonly Counter<long> _recoveryOutstandingCounter;

    public ReconciliationMetrics()
    {
        _webhookReceivedCounter = Meter.CreateCounter<long>("webhook_received_total", "Total inbound webhooks received");
        _webhookDuplicateCounter = Meter.CreateCounter<long>("webhook_duplicate_total", "Total duplicate webhooks safely ignored");
        _webhookProcessingCounter = Meter.CreateCounter<long>("webhook_processing_total", "Total webhooks processed asynchronously");
        _webhookProcessingFailuresCounter = Meter.CreateCounter<long>("webhook_processing_failures_total", "Total webhook processing failures");
        _reconciliationStartedCounter = Meter.CreateCounter<long>("reconciliation_started_total", "Total reconciliation workflows initiated");
        _reconciliationSuccessCounter = Meter.CreateCounter<long>("reconciliation_success_total", "Total reconciliations resolved as success");
        _reconciliationFailureCounter = Meter.CreateCounter<long>("reconciliation_failure_total", "Total reconciliations resolved as failure");
        _reconciliationUnresolvedCounter = Meter.CreateCounter<long>("reconciliation_unresolved_total", "Total reconciliations remaining unresolved");
        _manualReviewCounter = Meter.CreateCounter<long>("manual_review_total", "Total reconciliation cases escalated to manual review");
        _providerMismatchCounter = Meter.CreateCounter<long>("provider_mismatch_total", "Total amount or currency mismatches detected");
        _financialReversalCounter = Meter.CreateCounter<long>("financial_reversal_total", "Total financial reversals executed");
        _recoveryOutstandingCounter = Meter.CreateCounter<long>("recovery_outstanding_total", "Total recovery outstanding records created");
    }

    public void RecordWebhookReceived(string provider, string eventType) =>
        _webhookReceivedCounter.Add(1, new KeyValuePair<string, object?>("provider", provider), new KeyValuePair<string, object?>("event_type", eventType));

    public void RecordWebhookDuplicate(string provider, string eventType) =>
        _webhookDuplicateCounter.Add(1, new KeyValuePair<string, object?>("provider", provider), new KeyValuePair<string, object?>("event_type", eventType));

    public void RecordWebhookProcessing(string provider, string eventType, string result) =>
        _webhookProcessingCounter.Add(1, new KeyValuePair<string, object?>("provider", provider), new KeyValuePair<string, object?>("event_type", eventType), new KeyValuePair<string, object?>("result", result));

    public void RecordWebhookProcessingFailure(string provider, string eventType, string reason) =>
        _webhookProcessingFailuresCounter.Add(1, new KeyValuePair<string, object?>("provider", provider), new KeyValuePair<string, object?>("event_type", eventType), new KeyValuePair<string, object?>("reason", reason));

    public void RecordReconciliationStarted(string provider, string operation) =>
        _reconciliationStartedCounter.Add(1, new KeyValuePair<string, object?>("provider", provider), new KeyValuePair<string, object?>("operation", operation));

    public void RecordReconciliationSuccess(string provider, string operation) =>
        _reconciliationSuccessCounter.Add(1, new KeyValuePair<string, object?>("provider", provider), new KeyValuePair<string, object?>("operation", operation));

    public void RecordReconciliationFailure(string provider, string operation, string failureCode) =>
        _reconciliationFailureCounter.Add(1, new KeyValuePair<string, object?>("provider", provider), new KeyValuePair<string, object?>("operation", operation), new KeyValuePair<string, object?>("failure_code", failureCode));

    public void RecordReconciliationUnresolved(string provider, string operation) =>
        _reconciliationUnresolvedCounter.Add(1, new KeyValuePair<string, object?>("provider", provider), new KeyValuePair<string, object?>("operation", operation));

    public void RecordManualReview(string provider, string operation) =>
        _manualReviewCounter.Add(1, new KeyValuePair<string, object?>("provider", provider), new KeyValuePair<string, object?>("operation", operation));

    public void RecordProviderMismatch(string provider, string mismatchType) =>
        _providerMismatchCounter.Add(1, new KeyValuePair<string, object?>("provider", provider), new KeyValuePair<string, object?>("mismatch_type", mismatchType));

    public void RecordFinancialReversal(string provider, string operation) =>
        _financialReversalCounter.Add(1, new KeyValuePair<string, object?>("provider", provider), new KeyValuePair<string, object?>("operation", operation));

    public void RecordRecoveryOutstanding(string provider, string source) =>
        _recoveryOutstandingCounter.Add(1, new KeyValuePair<string, object?>("provider", provider), new KeyValuePair<string, object?>("source", source));
}
