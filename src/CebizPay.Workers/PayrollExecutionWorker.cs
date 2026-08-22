using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payroll;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Payroll.Entities;
using CebizPay.Domain.Payroll.Enums;
using CebizPay.Domain.Payroll.Events;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Workers;

/// <summary>
/// Background worker for executing corporate payroll batches asynchronously with bounded concurrency,
/// database-safe item claiming, atomic per-item financial settlement, and automatic stale-item recovery.
/// </summary>
public sealed partial class PayrollExecutionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PayrollExecutionWorker> _logger;
    private readonly string _workerId;
    private const int MaxConcurrency = 5;
    private static readonly TimeSpan StaleProcessingTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initializes a new instance of <see cref="PayrollExecutionWorker"/>.
    /// </summary>
    public PayrollExecutionWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PayrollExecutionWorker> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _workerId = $"PayrollWorker-{Guid.NewGuid().ToString("N")[..8]}";
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkerStarted(_logger, _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            var processedAny = false;
            try
            {
                processedAny = await ProcessNextPayrollBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogWorkerLoopError(_logger, ex);
            }

            var delay = processedAny ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromSeconds(3);
            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
        }

        LogWorkerStopped(_logger, _workerId);
    }

    private async Task<bool> ProcessNextPayrollBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var executionService = scope.ServiceProvider.GetRequiredService<IPayrollExecutionService>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxService>();

        // 1. Recover stale processing items across batches (if worker previously crashed)
        await RecoverStaleItemsAsync(dbContext, cancellationToken).ConfigureAwait(false);

        // 2. Claim active batch
        var batch = await dbContext.PayrollBatches
            .Where(b => b.Status == PayrollBatchStatus.Pending || b.Status == PayrollBatchStatus.Processing)
            .OrderBy(b => b.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (batch == null)
            return false;

        // Transition Pending batch to Processing
        if (batch.Status == PayrollBatchStatus.Pending)
        {
            batch.MarkProcessing();

            var startAudit = AuditLog.Create(
                actorId: _workerId,
                action: AuditActions.PayrollStarted,
                resourceType: AuditResourceTypes.PayrollBatch,
                resourceId: batch.Id.ToString(),
                organizationId: batch.OrganizationId);
            dbContext.AuditLogs.Add(startAudit);

            outbox.Write(new PayrollBatchStartedDomainEvent(
                PayrollBatchId: batch.Id,
                BatchReference: batch.BatchReference,
                OrganizationId: batch.OrganizationId,
                OccurredOnUtc: DateTime.UtcNow));

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            LogBatchStarted(_logger, batch.BatchReference);
        }

        // 3. Fetch eligible items to process (Pending or RetryPending)
        var itemsToClaim = await dbContext.PayrollItems
            .Where(i => i.PayrollBatchId == batch.Id && (i.Status == PayrollItemStatus.Pending || i.Status == PayrollItemStatus.RetryPending))
            .OrderBy(i => i.CreatedAtUtc)
            .Take(MaxConcurrency)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (itemsToClaim.Count > 0)
        {
            // Claim items
            foreach (var item in itemsToClaim)
            {
                item.Claim(_workerId);
            }
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Execute claimed items concurrently with bounded concurrency
            using var semaphore = new SemaphoreSlim(MaxConcurrency);
            var tasks = itemsToClaim.Select(async item =>
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await executionService.ExecutePayrollItemAsync(item.Id, _workerId, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return true;
        }

        // 4. Check if any items are still in Processing status
        var activeProcessingCount = await dbContext.PayrollItems
            .CountAsync(i => i.PayrollBatchId == batch.Id && i.Status == PayrollItemStatus.Processing, cancellationToken)
            .ConfigureAwait(false);

        if (activeProcessingCount > 0)
        {
            // Items still in-flight
            return false;
        }

        // 5. Finalize batch status when all items reached a terminal or unresolved state
        var totalCount = await dbContext.PayrollItems.CountAsync(i => i.PayrollBatchId == batch.Id, cancellationToken).ConfigureAwait(false);
        var completedCount = await dbContext.PayrollItems.CountAsync(i => i.PayrollBatchId == batch.Id && i.Status == PayrollItemStatus.Completed, cancellationToken).ConfigureAwait(false);
        var failedCount = await dbContext.PayrollItems.CountAsync(i => i.PayrollBatchId == batch.Id && i.Status == PayrollItemStatus.Failed, cancellationToken).ConfigureAwait(false);

        if (completedCount == totalCount && totalCount > 0)
        {
            batch.MarkCompleted();

            var audit = AuditLog.Create(
                actorId: _workerId,
                action: AuditActions.PayrollCompleted,
                resourceType: AuditResourceTypes.PayrollBatch,
                resourceId: batch.Id.ToString(),
                organizationId: batch.OrganizationId);
            dbContext.AuditLogs.Add(audit);

            outbox.Write(new PayrollBatchCompletedDomainEvent(
                PayrollBatchId: batch.Id,
                BatchReference: batch.BatchReference,
                OrganizationId: batch.OrganizationId,
                TotalCompleted: completedCount,
                TotalDisbursed: batch.TotalNetAmount,
                Currency: batch.Currency,
                OccurredOnUtc: DateTime.UtcNow));

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            LogBatchCompleted(_logger, batch.BatchReference, completedCount);
            return true;
        }
        else if (completedCount > 0 && failedCount > 0)
        {
            batch.MarkPartiallyCompleted();

            var audit = AuditLog.Create(
                actorId: _workerId,
                action: AuditActions.PayrollPartiallyCompleted,
                resourceType: AuditResourceTypes.PayrollBatch,
                resourceId: batch.Id.ToString(),
                organizationId: batch.OrganizationId);
            dbContext.AuditLogs.Add(audit);

            outbox.Write(new PayrollBatchPartiallyCompletedDomainEvent(
                PayrollBatchId: batch.Id,
                BatchReference: batch.BatchReference,
                OrganizationId: batch.OrganizationId,
                CompletedCount: completedCount,
                FailedCount: failedCount,
                Currency: batch.Currency,
                OccurredOnUtc: DateTime.UtcNow));

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            LogBatchPartiallyCompleted(_logger, batch.BatchReference, completedCount, failedCount);
            return true;
        }
        else if (failedCount == totalCount && totalCount > 0)
        {
            var failReason = "All payroll line items encountered execution errors.";
            batch.MarkFailed(failReason);

            var audit = AuditLog.Create(
                actorId: _workerId,
                action: AuditActions.PayrollFailed,
                resourceType: AuditResourceTypes.PayrollBatch,
                resourceId: batch.Id.ToString(),
                organizationId: batch.OrganizationId);
            dbContext.AuditLogs.Add(audit);

            outbox.Write(new PayrollBatchFailedDomainEvent(
                PayrollBatchId: batch.Id,
                BatchReference: batch.BatchReference,
                OrganizationId: batch.OrganizationId,
                FailureReason: failReason,
                OccurredOnUtc: DateTime.UtcNow));

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            LogBatchFailed(_logger, batch.BatchReference, failReason);
            return true;
        }

        return false;
    }

    private async Task RecoverStaleItemsAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var staleThreshold = DateTime.UtcNow - StaleProcessingTimeout;
        var staleItems = await dbContext.PayrollItems
            .Where(i => i.Status == PayrollItemStatus.Processing && i.ClaimedAtUtc < staleThreshold)
            .Take(20)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (staleItems.Count == 0)
            return;

        foreach (var item in staleItems)
        {
            item.MarkFailed("STALE_PROCESSING_TIMEOUT", "Worker heartbeat expired during execution attempt.");
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogStaleItemsReclaimed(_logger, staleItems.Count);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "PayrollExecutionWorker {WorkerId} started.")]
    private static partial void LogWorkerStarted(ILogger logger, string workerId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "PayrollExecutionWorker {WorkerId} stopped.")]
    private static partial void LogWorkerStopped(ILogger logger, string workerId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Unhandled exception in PayrollExecutionWorker loop.")]
    private static partial void LogWorkerLoopError(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Started processing PayrollBatch {BatchReference}")]
    private static partial void LogBatchStarted(ILogger logger, string batchReference);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Completed PayrollBatch {BatchReference} with {CompletedCount} items.")]
    private static partial void LogBatchCompleted(ILogger logger, string batchReference, int completedCount);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "PayrollBatch {BatchReference} partially completed: {CompletedCount} succeeded, {FailedCount} failed.")]
    private static partial void LogBatchPartiallyCompleted(ILogger logger, string batchReference, int completedCount, int failedCount);

    [LoggerMessage(EventId = 7, Level = LogLevel.Error, Message = "PayrollBatch {BatchReference} failed completely: {Reason}")]
    private static partial void LogBatchFailed(ILogger logger, string batchReference, string reason);

    [LoggerMessage(EventId = 8, Level = LogLevel.Warning, Message = "Reclaimed {Count} stale processing payroll item(s).")]
    private static partial void LogStaleItemsReclaimed(ILogger logger, int count);
}
