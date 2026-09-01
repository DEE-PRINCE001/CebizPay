#pragma warning disable CS1591
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Domain.Payments.Entities;

/// <summary>
/// Domain entity tracking the asynchronous reconciliation lifecycle between internal CebizPay
/// financial/compliance operations and external provider gateways.
/// Hardens fail-closed semantics, prevents duplicate financial movements, and guarantees UNKNOWN safety.
/// </summary>
public sealed class ReconciliationRecord
{
    public const int DefaultMaxAttempts = 5;

    private ReconciliationRecord() { } // EF Core

    public Guid Id { get; private set; }
    public ReconciliationType ReconciliationType { get; private set; }
    public string SourceReference { get; private set; } = string.Empty;
    public string Provider { get; private set; } = string.Empty;
    public string? ProviderReference { get; private set; }
    public decimal? ExpectedAmount { get; private set; }
    public decimal? ReconciledAmount { get; private set; }
    public Currency? Currency { get; private set; }
    public ReconciliationStatus Status { get; private set; }
    public string? DiscrepancyReason { get; private set; }
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; } = DefaultMaxAttempts;
    public DateTime? NextPollAtUtc { get; private set; }
    public DateTime? LastPolledAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public string? SafeMetadata { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static ReconciliationRecord Create(
        ReconciliationType reconciliationType,
        string sourceReference,
        string provider,
        string? providerReference = null,
        decimal? expectedAmount = null,
        Currency? currency = null,
        string? safeMetadata = null,
        int maxAttempts = DefaultMaxAttempts)
    {
        if (string.IsNullOrWhiteSpace(sourceReference))
            throw new ArgumentException("SourceReference is required.", nameof(sourceReference));
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider is required.", nameof(provider));

        var now = DateTime.UtcNow;
        return new ReconciliationRecord
        {
            Id = Guid.NewGuid(),
            ReconciliationType = reconciliationType,
            SourceReference = sourceReference.Trim(),
            Provider = provider.Trim(),
            ProviderReference = providerReference?.Trim(),
            ExpectedAmount = expectedAmount,
            Currency = currency,
            Status = ReconciliationStatus.Pending,
            AttemptCount = 0,
            MaxAttempts = maxAttempts > 0 ? maxAttempts : DefaultMaxAttempts,
            NextPollAtUtc = now,
            SafeMetadata = safeMetadata,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void MarkInProgress()
    {
        var now = DateTime.UtcNow;
        Status = ReconciliationStatus.InProgress;
        AttemptCount++;
        LastPolledAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void MarkSuccess(decimal? reconciledAmount = null, string? providerReference = null, string? safeMetadata = null)
    {
        var now = DateTime.UtcNow;
        Status = ReconciliationStatus.ResolvedSuccess;
        ReconciledAmount = reconciledAmount ?? ExpectedAmount;
        if (!string.IsNullOrWhiteSpace(providerReference))
            ProviderReference = providerReference.Trim();
        if (!string.IsNullOrWhiteSpace(safeMetadata))
            SafeMetadata = safeMetadata;
        ResolvedAtUtc = now;
        UpdatedAtUtc = now;
        NextPollAtUtc = null;
    }

    public void MarkFailure(string failureReason, string? safeMetadata = null)
    {
        var now = DateTime.UtcNow;
        Status = ReconciliationStatus.ResolvedFailure;
        DiscrepancyReason = failureReason?.Trim();
        if (!string.IsNullOrWhiteSpace(safeMetadata))
            SafeMetadata = safeMetadata;
        ResolvedAtUtc = now;
        UpdatedAtUtc = now;
        NextPollAtUtc = null;
    }

    public void MarkReversed(string reason, string? safeMetadata = null)
    {
        var now = DateTime.UtcNow;
        Status = ReconciliationStatus.ResolvedReversed;
        DiscrepancyReason = reason?.Trim();
        if (!string.IsNullOrWhiteSpace(safeMetadata))
            SafeMetadata = safeMetadata;
        ResolvedAtUtc = now;
        UpdatedAtUtc = now;
        NextPollAtUtc = null;
    }

    public void ScheduleNextPoll(TimeSpan delay, string? interimReason = null)
    {
        var now = DateTime.UtcNow;
        if (AttemptCount >= MaxAttempts)
        {
            Status = ReconciliationStatus.Unresolved;
            DiscrepancyReason = interimReason ?? $"Maximum status polling attempts ({MaxAttempts}) exceeded without definitive provider outcome.";
            NextPollAtUtc = null;
        }
        else
        {
            Status = ReconciliationStatus.Pending;
            NextPollAtUtc = now.Add(delay);
            if (!string.IsNullOrWhiteSpace(interimReason))
                DiscrepancyReason = interimReason.Trim();
        }
        UpdatedAtUtc = now;
    }

    public void MarkManualReview(string reason, string? safeMetadata = null)
    {
        var now = DateTime.UtcNow;
        Status = ReconciliationStatus.ManualReview;
        DiscrepancyReason = reason?.Trim();
        if (!string.IsNullOrWhiteSpace(safeMetadata))
            SafeMetadata = safeMetadata;
        ResolvedAtUtc = now;
        UpdatedAtUtc = now;
        NextPollAtUtc = null;
    }

    public void MarkFailedPermanently(string reason)
    {
        var now = DateTime.UtcNow;
        Status = ReconciliationStatus.FailedPermanently;
        DiscrepancyReason = reason?.Trim();
        ResolvedAtUtc = now;
        UpdatedAtUtc = now;
        NextPollAtUtc = null;
    }
}
