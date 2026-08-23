using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Vas;
using CebizPay.Application.Common.Models.Vas;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Vas.Entities;
using CebizPay.Domain.Vas.Enums;
using CebizPay.Domain.Vas.Events;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Vas;

/// <summary>
/// Service responsible for reconciling unresolved (Unknown / stale Processing) VAS transactions
/// by querying VTUGATE status and executing finalizations or financial reversals.
/// </summary>
public sealed partial class VasReconciliationService : IVasReconciliationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IVasProvider _vasProvider;
    private readonly ILedgerPostingService _ledgerService;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<VasReconciliationService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="VasReconciliationService"/>.
    /// </summary>
    public VasReconciliationService(
        ApplicationDbContext dbContext,
        IVasProvider vasProvider,
        ILedgerPostingService ledgerService,
        IOutboxService outboxService,
        ILogger<VasReconciliationService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _vasProvider = vasProvider ?? throw new ArgumentNullException(nameof(vasProvider));
        _ledgerService = ledgerService ?? throw new ArgumentNullException(nameof(ledgerService));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<VasPurchaseProviderResult> ReconcileVasTransactionAsync(Guid vasTransactionId, CancellationToken cancellationToken = default)
    {
        var txn = await _dbContext.VasTransactions
            .FirstOrDefaultAsync(t => t.Id == vasTransactionId, cancellationToken)
            .ConfigureAwait(false);

        if (txn == null)
        {
            return VasPurchaseProviderResult.BusinessFailure("NOT_FOUND", "VAS transaction not found.");
        }

        if (txn.Status is VasTransactionStatus.Succeeded or VasTransactionStatus.Reversed)
        {
            return VasPurchaseProviderResult.Success(txn.ProviderReference ?? txn.Reference);
        }

        LogReconciliationStarted(_logger, txn.Reference, txn.Status);

        var previousStatus = txn.Status;
        var statusResult = await _vasProvider.GetTransactionStatusAsync(txn.Reference, txn.ProviderReference, cancellationToken).ConfigureAwait(false);

        switch (statusResult.Status)
        {
            case VasPurchaseResultStatus.Success:
                var provRef = statusResult.ProviderReference ?? txn.Reference;
                txn.MarkSucceeded(provRef);

                _outboxService.Write(new VasPurchaseSucceededEvent(
                    txn.Id,
                    txn.Reference,
                    provRef,
                    txn.Amount,
                    txn.Currency.ToString(),
                    DateTime.UtcNow));

                _outboxService.Write(new VasPurchaseReconciledEvent(
                    txn.Id,
                    txn.Reference,
                    previousStatus,
                    VasTransactionStatus.Succeeded,
                    provRef,
                    DateTime.UtcNow));

                var auditLogSuccess = AuditLog.Create(
                    actorId: "SYSTEM_RECONCILIATION",
                    action: Domain.Auditing.AuditActions.VasPurchaseReconciled,
                    resourceType: Domain.Auditing.AuditResourceTypes.VasTransaction,
                    resourceId: txn.Id.ToString(),
                    organizationId: txn.OrganizationId,
                    afterJson: JsonSerializer.Serialize(new
                    {
                        txn.Reference,
                        ProviderReference = provRef,
                        PreviousStatus = previousStatus.ToString(),
                        NewStatus = VasTransactionStatus.Succeeded.ToString()
                    }));

                _dbContext.AuditLogs.Add(auditLogSuccess);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                LogReconciliationResolved(_logger, txn.Reference, "SUCCEEDED");
                return statusResult;

            case VasPurchaseResultStatus.BusinessFailure:
                var failureReason = statusResult.FailureReason ?? "Reconciliation confirmed transaction failure.";
                await _ledgerService.PostVasPurchaseReversalCoreAsync(txn.Id, failureReason, cancellationToken).ConfigureAwait(false);

                _outboxService.Write(new VasPurchaseReversedEvent(
                    txn.Id,
                    txn.Reference,
                    failureReason,
                    txn.Amount,
                    txn.Currency.ToString(),
                    DateTime.UtcNow));

                _outboxService.Write(new VasPurchaseReconciledEvent(
                    txn.Id,
                    txn.Reference,
                    previousStatus,
                    VasTransactionStatus.Reversed,
                    statusResult.ProviderReference,
                    DateTime.UtcNow));

                var auditLogReversed = AuditLog.Create(
                    actorId: "SYSTEM_RECONCILIATION",
                    action: Domain.Auditing.AuditActions.VasPurchaseReconciled,
                    resourceType: Domain.Auditing.AuditResourceTypes.VasTransaction,
                    resourceId: txn.Id.ToString(),
                    organizationId: txn.OrganizationId,
                    afterJson: JsonSerializer.Serialize(new
                    {
                        txn.Reference,
                        PreviousStatus = previousStatus.ToString(),
                        NewStatus = VasTransactionStatus.Reversed.ToString(),
                        Reason = failureReason
                    }));

                _dbContext.AuditLogs.Add(auditLogReversed);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                LogReconciliationResolved(_logger, txn.Reference, "REVERSED");
                return statusResult;

            case VasPurchaseResultStatus.TechnicalFailure:
            case VasPurchaseResultStatus.Unknown:
            default:
                LogReconciliationUnresolved(_logger, txn.Reference, statusResult.FailureReason ?? "Still in-flight");
                return statusResult;
        }
    }

    /// <inheritdoc/>
    public async Task<int> ReconcileUnresolvedVasTransactionsAsync(int batchSize = 50, CancellationToken cancellationToken = default)
    {
        var staleThreshold = DateTime.UtcNow.AddMinutes(-2);

        var unresolved = await _dbContext.VasTransactions
            .Where(t => t.Status == VasTransactionStatus.Unknown ||
                       (t.Status == VasTransactionStatus.Processing && t.CreatedAtUtc <= staleThreshold))
            .OrderBy(t => t.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (unresolved.Count == 0)
        {
            return 0;
        }

        int resolvedCount = 0;
        foreach (var txn in unresolved)
        {
            try
            {
                var res = await ReconcileVasTransactionAsync(txn.Id, cancellationToken).ConfigureAwait(false);
                if (res.Status is VasPurchaseResultStatus.Success or VasPurchaseResultStatus.BusinessFailure)
                {
                    resolvedCount++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogReconciliationError(_logger, txn.Reference, ex.Message);
            }
        }

        return resolvedCount;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Starting status reconciliation for VAS transaction {Reference} (Current status: {Status})")]
    private static partial void LogReconciliationStarted(ILogger logger, string reference, VasTransactionStatus status);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "VAS transaction {Reference} reconciled to final state: {FinalStatus}")]
    private static partial void LogReconciliationResolved(ILogger logger, string reference, string finalStatus);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "VAS transaction {Reference} status remains unresolved: {Reason}")]
    private static partial void LogReconciliationUnresolved(ILogger logger, string reference, string reason);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Error reconciling VAS transaction {Reference}: {ErrorMessage}")]
    private static partial void LogReconciliationError(ILogger logger, string reference, string errorMessage);
}
