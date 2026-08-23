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
/// Service responsible for executing external VAS fulfillment with VTUGATE
/// and orchestrating state transitions, automated financial reversals, and outbox notifications.
/// </summary>
public sealed partial class VasPurchaseExecutor : IVasPurchaseExecutor
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IVasProvider _vasProvider;
    private readonly ILedgerPostingService _ledgerService;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<VasPurchaseExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="VasPurchaseExecutor"/>.
    /// </summary>
    public VasPurchaseExecutor(
        ApplicationDbContext dbContext,
        IVasProvider vasProvider,
        ILedgerPostingService ledgerService,
        IOutboxService outboxService,
        ILogger<VasPurchaseExecutor> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _vasProvider = vasProvider ?? throw new ArgumentNullException(nameof(vasProvider));
        _ledgerService = ledgerService ?? throw new ArgumentNullException(nameof(ledgerService));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<VasPurchaseResult> ExecutePurchaseAsync(Guid vasTransactionId, CancellationToken cancellationToken = default)
    {
        var txn = await _dbContext.VasTransactions
            .FirstOrDefaultAsync(t => t.Id == vasTransactionId, cancellationToken)
            .ConfigureAwait(false);

        if (txn == null)
        {
            LogTransactionNotFound(_logger, vasTransactionId);
            return new VasPurchaseResult(false, VasTransactionStatus.Failed, string.Empty, null, "VAS transaction not found.");
        }

        if (txn.Status is VasTransactionStatus.Succeeded or VasTransactionStatus.Reversed)
        {
            return new VasPurchaseResult(
                txn.Status == VasTransactionStatus.Succeeded,
                txn.Status,
                txn.Reference,
                txn.ProviderReference,
                txn.FailureReason);
        }

        // Mark processing
        txn.MarkProcessing();
        _outboxService.Write(new VasPurchaseProcessingEvent(txn.Id, txn.Reference, _vasProvider.Provider, DateTime.UtcNow));
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogFulfillmentDispatchStarted(_logger, txn.Reference, txn.Type, txn.Network, txn.Amount);

        // Execute external provider call outside DB transaction
        VasPurchaseProviderResult providerResult;
        if (txn.Type == VasType.Airtime)
        {
            providerResult = await _vasProvider.PurchaseAirtimeAsync(
                txn.Reference,
                txn.PhoneNumber,
                txn.Network,
                txn.Amount,
                txn.Currency,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            providerResult = await _vasProvider.PurchaseDataAsync(
                txn.Reference,
                txn.PhoneNumber,
                txn.Network,
                txn.ProductCode ?? string.Empty,
                txn.Amount,
                txn.Currency,
                cancellationToken).ConfigureAwait(false);
        }

        // Handle provider outcome
        switch (providerResult.Status)
        {
            case VasPurchaseResultStatus.Success:
                return await FinalizeSuccessAsync(txn, providerResult.ProviderReference ?? txn.Reference, cancellationToken).ConfigureAwait(false);

            case VasPurchaseResultStatus.BusinessFailure:
                return await FinalizeBusinessFailureAsync(txn, providerResult.FailureCode, providerResult.FailureReason ?? "Provider rejected VAS purchase.", cancellationToken).ConfigureAwait(false);

            case VasPurchaseResultStatus.TechnicalFailure:
            case VasPurchaseResultStatus.Unknown:
            default:
                return await HandleUnknownOutcomeAsync(txn, providerResult.FailureReason ?? "Provider fulfillment is indeterminate / timed out.", cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<VasPurchaseResult> FinalizeSuccessAsync(VasTransaction txn, string providerReference, CancellationToken cancellationToken)
    {
        txn.MarkSucceeded(providerReference);

        _outboxService.Write(new VasPurchaseSucceededEvent(
            txn.Id,
            txn.Reference,
            providerReference,
            txn.Amount,
            txn.Currency.ToString(),
            DateTime.UtcNow));

        var auditLog = AuditLog.Create(
            actorId: txn.UserId,
            action: Domain.Auditing.AuditActions.VasPurchaseSucceeded,
            resourceType: Domain.Auditing.AuditResourceTypes.VasTransaction,
            resourceId: txn.Id.ToString(),
            organizationId: txn.OrganizationId,
            afterJson: JsonSerializer.Serialize(new
            {
                txn.Reference,
                ProviderReference = providerReference,
                txn.Amount,
                Currency = txn.Currency.ToString(),
                Status = VasTransactionStatus.Succeeded.ToString()
            }));

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogFulfillmentSucceeded(_logger, txn.Reference, providerReference);
        return new VasPurchaseResult(true, VasTransactionStatus.Succeeded, txn.Reference, providerReference, null);
    }

    private async Task<VasPurchaseResult> FinalizeBusinessFailureAsync(VasTransaction txn, string? failureCode, string failureReason, CancellationToken cancellationToken)
    {
        LogFulfillmentDefinitiveFailure(_logger, txn.Reference, failureReason);

        // Perform atomic ledger reversal
        await _ledgerService.PostVasPurchaseReversalCoreAsync(txn.Id, failureReason, cancellationToken).ConfigureAwait(false);

        _outboxService.Write(new VasPurchaseFailedEvent(
            txn.Id,
            txn.Reference,
            failureCode,
            failureReason,
            DateTime.UtcNow));

        _outboxService.Write(new VasPurchaseReversedEvent(
            txn.Id,
            txn.Reference,
            failureReason,
            txn.Amount,
            txn.Currency.ToString(),
            DateTime.UtcNow));

        var auditLog = AuditLog.Create(
            actorId: txn.UserId,
            action: Domain.Auditing.AuditActions.VasPurchaseReversed,
            resourceType: Domain.Auditing.AuditResourceTypes.VasTransaction,
            resourceId: txn.Id.ToString(),
            organizationId: txn.OrganizationId,
            afterJson: JsonSerializer.Serialize(new
            {
                txn.Reference,
                txn.Amount,
                Currency = txn.Currency.ToString(),
                Status = VasTransactionStatus.Reversed.ToString(),
                FailureReason = failureReason
            }));

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new VasPurchaseResult(false, VasTransactionStatus.Reversed, txn.Reference, null, failureReason);
    }

    private async Task<VasPurchaseResult> HandleUnknownOutcomeAsync(VasTransaction txn, string reason, CancellationToken cancellationToken)
    {
        LogFulfillmentIndeterminate(_logger, txn.Reference, reason);

        txn.MarkUnknown(reason);

        _outboxService.Write(new VasPurchaseUnknownEvent(
            txn.Id,
            txn.Reference,
            reason,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new VasPurchaseResult(false, VasTransactionStatus.Unknown, txn.Reference, null, reason);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "VAS Transaction with ID {VasTransactionId} was not found for execution.")]
    private static partial void LogTransactionNotFound(ILogger logger, Guid vasTransactionId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Dispatching VAS fulfillment for reference {Reference}, type {Type}, network {Network}, amount ₦{Amount}")]
    private static partial void LogFulfillmentDispatchStarted(ILogger logger, string reference, VasType type, VasNetwork network, decimal amount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "VAS fulfillment for reference {Reference} succeeded with provider ref {ProviderReference}")]
    private static partial void LogFulfillmentSucceeded(ILogger logger, string reference, string providerReference);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "VAS fulfillment for reference {Reference} failed definitively: {FailureReason}. Initiating automatic financial reversal.")]
    private static partial void LogFulfillmentDefinitiveFailure(ILogger logger, string reference, string failureReason);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "VAS fulfillment for reference {Reference} is indeterminate: {Reason}. Scheduled for background reconciliation.")]
    private static partial void LogFulfillmentIndeterminate(ILogger logger, string reference, string reason);
}
