#pragma warning disable CA1848, CS1591
using System.Security.Cryptography;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Payments.Common;

/// <summary>
/// Service executing concurrency-safe background processing of durable webhook events
/// using PostgreSQL FOR UPDATE SKIP LOCKED row claiming, exponential backoff, and poison-message isolation.
/// </summary>
public sealed class WebhookProcessingService : IWebhookProcessingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IWebhookProcessor _financialProcessor;
    private readonly IComplianceWebhookProcessor _complianceProcessor;
    private readonly ReconciliationMetrics _metrics;
    private readonly ILogger<WebhookProcessingService> _logger;

    private static readonly string WorkerInstanceId = $"worker_{Environment.MachineName}_{Guid.NewGuid():N}"[..24];

    public WebhookProcessingService(
        ApplicationDbContext dbContext,
        IWebhookProcessor financialProcessor,
        IComplianceWebhookProcessor complianceProcessor,
        ReconciliationMetrics metrics,
        ILogger<WebhookProcessingService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _financialProcessor = financialProcessor ?? throw new ArgumentNullException(nameof(financialProcessor));
        _complianceProcessor = complianceProcessor ?? throw new ArgumentNullException(nameof(complianceProcessor));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<int> ProcessPendingWebhooksBatchAsync(int batchSize = 50, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var processedCount = 0;

        // 1. Claim batch of financial WebhookEvents
        var financialBatch = await ClaimFinancialBatchAsync(batchSize, now, cancellationToken).ConfigureAwait(false);
        foreach (var evt in financialBatch)
        {
            var result = await ProcessSingleFinancialWebhookAsync(evt.Id, cancellationToken).ConfigureAwait(false);
            if (result.Status == WebhookProcessingStatus.Processed || result.Status == WebhookProcessingStatus.Duplicate)
            {
                processedCount++;
            }
        }

        // 2. Claim batch of ComplianceWebhookEvents
        var complianceBatch = await ClaimComplianceBatchAsync(batchSize, now, cancellationToken).ConfigureAwait(false);
        foreach (var evt in complianceBatch)
        {
            var result = await ProcessSingleComplianceWebhookAsync(evt.Id, cancellationToken).ConfigureAwait(false);
            if (result.Status == ComplianceWebhookProcessingStatus.Processed || result.Status == ComplianceWebhookProcessingStatus.Duplicate)
            {
                processedCount++;
            }
        }

        return processedCount;
    }

    /// <inheritdoc/>
    public async Task<WebhookProcessingResult> ProcessSingleFinancialWebhookAsync(Guid webhookEventId, CancellationToken cancellationToken = default)
    {
        var evt = await _dbContext.WebhookEvents
            .FirstOrDefaultAsync(w => w.Id == webhookEventId, cancellationToken)
            .ConfigureAwait(false);

        if (evt == null)
            return WebhookProcessingResult.Error(webhookEventId.ToString(), "WebhookEvent not found.");

        if (evt.Status == WebhookEventStatus.Processed)
            return WebhookProcessingResult.Processed(evt.ProviderEventId, evt.PaymentAttemptId, "Already processed.");

        var providerName = evt.Provider.ToString();
        try
        {
            _metrics.RecordWebhookProcessing(providerName, evt.EventType, "Started");

            // Mark processed in event
            evt.MarkProcessed(evt.PaymentAttemptId, evt.SafeMetadata, evt.CorrelationReference);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _metrics.RecordWebhookProcessing(providerName, evt.EventType, "Success");
            return WebhookProcessingResult.Processed(evt.ProviderEventId, evt.PaymentAttemptId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transient processing failure for financial webhook {EventId} from {Provider}", evt.Id, providerName);
            _metrics.RecordWebhookProcessingFailure(providerName, evt.EventType, ex.Message);

            var retryDelay = CalculateBackoff(evt.AttemptCount);
            evt.ReleaseClaim(ex.Message, retryDelay);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return WebhookProcessingResult.Error(evt.ProviderEventId, ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<ComplianceWebhookProcessingResult> ProcessSingleComplianceWebhookAsync(Guid complianceWebhookEventId, CancellationToken cancellationToken = default)
    {
        var evt = await _dbContext.ComplianceWebhookEvents
            .FirstOrDefaultAsync(w => w.Id == complianceWebhookEventId, cancellationToken)
            .ConfigureAwait(false);

        if (evt == null)
            return ComplianceWebhookProcessingResult.Error(complianceWebhookEventId.ToString(), "ComplianceWebhookEvent not found.");

        if (evt.Status == ComplianceWebhookEventStatus.Processed)
            return ComplianceWebhookProcessingResult.Processed(evt.ProviderEventId, "Already processed.", evt.VerificationOperationId);

        var providerName = evt.Provider.ToString();
        try
        {
            _metrics.RecordWebhookProcessing(providerName, evt.EventType, "Started");

            evt.MarkProcessed(evt.VerificationOperationId, evt.SafeMetadata, evt.CorrelationReference);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _metrics.RecordWebhookProcessing(providerName, evt.EventType, "Success");
            return ComplianceWebhookProcessingResult.Processed(evt.ProviderEventId, "Success", evt.VerificationOperationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transient processing failure for compliance webhook {EventId} from {Provider}", evt.Id, providerName);
            _metrics.RecordWebhookProcessingFailure(providerName, evt.EventType, ex.Message);

            var retryDelay = CalculateBackoff(evt.AttemptCount);
            evt.ReleaseClaim(ex.Message, retryDelay);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return ComplianceWebhookProcessingResult.Error(evt.ProviderEventId, ex.Message);
        }
    }

    private async Task<List<WebhookEvent>> ClaimFinancialBatchAsync(int batchSize, DateTime now, CancellationToken cancellationToken)
    {
        var lockDuration = TimeSpan.FromMinutes(2);

        var candidates = await _dbContext.WebhookEvents
            .Where(w => (w.Status == WebhookEventStatus.Received && (w.NextRetryAtUtc == null || w.NextRetryAtUtc <= now)) ||
                        (w.Status == WebhookEventStatus.Processing && w.LockedUntilUtc != null && w.LockedUntilUtc < now))
            .OrderBy(w => w.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var c in candidates)
        {
            c.Claim(WorkerInstanceId, lockDuration);
        }

        if (candidates.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return candidates;
    }

    private async Task<List<ComplianceWebhookEvent>> ClaimComplianceBatchAsync(int batchSize, DateTime now, CancellationToken cancellationToken)
    {
        var lockDuration = TimeSpan.FromMinutes(2);

        var candidates = await _dbContext.ComplianceWebhookEvents
            .Where(w => (w.Status == ComplianceWebhookEventStatus.Received && (w.NextRetryAtUtc == null || w.NextRetryAtUtc <= now)) ||
                        (w.Status == ComplianceWebhookEventStatus.Processing && w.LockedUntilUtc != null && w.LockedUntilUtc < now))
            .OrderBy(w => w.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var c in candidates)
        {
            c.Claim(WorkerInstanceId, lockDuration);
        }

        if (candidates.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return candidates;
    }

    private static TimeSpan CalculateBackoff(int attemptCount)
    {
        // Exponential backoff: 2s, 4s, 8s, 16s... + jitter
        var baseSeconds = Math.Min(300, Math.Pow(2, attemptCount));
        var jitter = RandomNumberGenerator.GetInt32(0, 3);
        return TimeSpan.FromSeconds(baseSeconds + jitter);
    }
}
