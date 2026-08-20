using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Finance.Events;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Payments.Events;
using CebizPay.Infrastructure.Payments.Flutterwave;
using CebizPay.Infrastructure.Payments.Paystack;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Payments.Common;

/// <summary>
/// Infrastructure implementation of <see cref="IWebhookProcessor"/> coordinating secure ingestion,
/// deduplication, state transition validation, and financial reconciliation for external payment webhooks.
/// </summary>
public sealed partial class WebhookProcessor : IWebhookProcessor
{
    private readonly IWebhookSignatureVerifier _signatureVerifier;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILedgerPostingService _ledgerPostingService;
    private readonly IOutboxService _outboxService;
    private readonly FlutterwaveOptions _flwOptions;
    private readonly PaystackOptions _pstkOptions;
    private readonly ILogger<WebhookProcessor> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookProcessor"/> class.
    /// </summary>
    public WebhookProcessor(
        IWebhookSignatureVerifier signatureVerifier,
        ApplicationDbContext dbContext,
        ILedgerPostingService ledgerPostingService,
        IOutboxService outboxService,
        IOptions<FlutterwaveOptions> flwOptions,
        IOptions<PaystackOptions> pstkOptions,
        ILogger<WebhookProcessor> logger)
    {
        _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _ledgerPostingService = ledgerPostingService ?? throw new ArgumentNullException(nameof(ledgerPostingService));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _flwOptions = flwOptions?.Value ?? throw new ArgumentNullException(nameof(flwOptions));
        _pstkOptions = pstkOptions?.Value ?? throw new ArgumentNullException(nameof(pstkOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<WebhookProcessingResult> ProcessWebhookAsync(
        PaymentProvider provider,
        string rawPayload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            return WebhookProcessingResult.InvalidPayload("Empty or missing webhook request body.");
        }

        var providerName = provider.ToString();

        // Step 1: Verify Provider Signature / Token
        var secret = provider switch
        {
            PaymentProvider.Flutterwave => !string.IsNullOrWhiteSpace(_flwOptions.WebhookSecretHash)
                ? _flwOptions.WebhookSecretHash
                : _flwOptions.SecretKey,
            PaymentProvider.Paystack => !string.IsNullOrWhiteSpace(_pstkOptions.WebhookSecret)
                ? _pstkOptions.WebhookSecret
                : _pstkOptions.SecretKey,
            _ => string.Empty
        };

        if (!_signatureVerifier.VerifySignature(provider, rawPayload, headers, secret))
        {
            LogWebhookSignatureFailed(_logger, providerName);
            RecordAudit(AuditActions.WebhookRejected, AuditResourceTypes.WebhookEvent, providerName,
                JsonSerializer.Serialize(new { Provider = providerName, Reason = "Signature verification failed" }));
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return WebhookProcessingResult.InvalidSignature();
        }

        // Step 2: Parse and normalize payload
        var payloadHash = ComputePayloadHash(rawPayload);
        var parsed = ParsePayload(provider, rawPayload);
        if (parsed == null || string.IsNullOrWhiteSpace(parsed.ProviderEventId))
        {
            LogWebhookMalformedPayload(_logger, providerName);
            return WebhookProcessingResult.InvalidPayload("Unable to parse mandatory event fields from webhook payload.");
        }

        // Step 3: Deduplication & Idempotent Ingestion
        var existingEvent = await _dbContext.WebhookEvents
            .FirstOrDefaultAsync(w => w.Provider == provider && w.ProviderEventId == parsed.ProviderEventId, cancellationToken)
            .ConfigureAwait(false);

        if (existingEvent != null)
        {
            LogWebhookDuplicate(_logger, providerName, parsed.ProviderEventId);
            RecordAudit(AuditActions.WebhookDuplicate, AuditResourceTypes.WebhookEvent, existingEvent.Id.ToString(),
                JsonSerializer.Serialize(new { Provider = providerName, ProviderEventId = parsed.ProviderEventId }));
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return WebhookProcessingResult.Duplicate(parsed.ProviderEventId);
        }

        // Persist initial WebhookEvent
        var webhookEvent = WebhookEvent.Create(
            provider: provider,
            providerEventId: parsed.ProviderEventId,
            eventType: parsed.EventType,
            payloadHash: payloadHash,
            safeMetadata: parsed.SafeMetadata);

        _dbContext.WebhookEvents.Add(webhookEvent);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Step 4: Resolve matching PaymentAttempt
        PaymentAttempt? attempt = null;
        if (!string.IsNullOrWhiteSpace(parsed.ProviderReference))
        {
            attempt = await _dbContext.PaymentAttempts
                .FirstOrDefaultAsync(p => p.Provider == provider && p.ProviderReference == parsed.ProviderReference, cancellationToken)
                .ConfigureAwait(false);
        }

        if (attempt == null && !string.IsNullOrWhiteSpace(parsed.Reference))
        {
            attempt = await _dbContext.PaymentAttempts
                .FirstOrDefaultAsync(p => p.RequestReference == parsed.Reference || p.RequestReference.Contains(parsed.Reference), cancellationToken)
                .ConfigureAwait(false);
        }

        if (attempt == null)
        {
            var parsedRef = parsed.Reference ?? string.Empty;
            LogWebhookAttemptNotFound(_logger, providerName, parsed.ProviderEventId, parsedRef);
            webhookEvent.MarkIgnored("No matching PaymentAttempt found for reference.");
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return WebhookProcessingResult.Ignored(parsed.ProviderEventId, "No matching PaymentAttempt found for reference.");
        }

        // Step 5: Amount and Currency Validation
        if (parsed.Amount.HasValue && parsed.Amount.Value > 0)
        {
            if (attempt.Amount != parsed.Amount.Value)
            {
                var errorMsg = string.Format(
                    CultureInfo.InvariantCulture,
                    "Amount mismatch. Attempt amount: {0}, Webhook amount: {1}",
                    attempt.Amount, parsed.Amount.Value);

                LogWebhookAmountMismatch(_logger, attempt.Id, attempt.Amount, parsed.Amount.Value);
                webhookEvent.MarkFailed(errorMsg);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return WebhookProcessingResult.Error(parsed.ProviderEventId, errorMsg);
            }
        }

        if (!string.IsNullOrWhiteSpace(parsed.Currency))
        {
            var attemptCurrencyStr = attempt.Currency.ToString();
            if (!string.Equals(attemptCurrencyStr, parsed.Currency, StringComparison.OrdinalIgnoreCase))
            {
                var errorMsg = string.Format(
                    CultureInfo.InvariantCulture,
                    "Currency mismatch. Attempt currency: {0}, Webhook currency: {1}",
                    attemptCurrencyStr, parsed.Currency);

                LogWebhookCurrencyMismatch(_logger, attempt.Id, attemptCurrencyStr, parsed.Currency);
                webhookEvent.MarkFailed(errorMsg);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return WebhookProcessingResult.Error(parsed.ProviderEventId, errorMsg);
            }
        }

        // Step 6: Safe State Machine Transition & Financial Reconciliation
        var bankTransfer = await _dbContext.BankTransfers
            .FirstOrDefaultAsync(b => b.LedgerTransactionId == attempt.LedgerTransactionId, cancellationToken)
            .ConfigureAwait(false);

        var prevStatus = attempt.Status;
        var attemptStatusStr = attempt.Status.ToString();

        // Ignore stale updates if already in terminal state
        if (attempt.Status == PaymentAttemptStatus.Succeeded)
        {
            LogWebhookStaleIgnored(_logger, attempt.Id, attemptStatusStr, parsed.ProviderStatus);
            webhookEvent.MarkProcessed(attempt.Id, parsed.SafeMetadata);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return WebhookProcessingResult.Processed(parsed.ProviderEventId, attempt.Id, "Attempt is already succeeded. Stale update ignored.");
        }

        if (attempt.Status == PaymentAttemptStatus.Failed && (parsed.IsSuccess || parsed.IsFailure))
        {
            LogWebhookStaleIgnored(_logger, attempt.Id, attemptStatusStr, parsed.ProviderStatus);
            webhookEvent.MarkProcessed(attempt.Id, parsed.SafeMetadata);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return WebhookProcessingResult.Processed(parsed.ProviderEventId, attempt.Id, "Attempt is already failed. Stale update ignored.");
        }

        if (parsed.IsSuccess)
        {
            // Provider confirms Success
            attempt.MarkSucceeded(parsed.ProviderReference ?? attempt.RequestReference, safeMetadata: parsed.SafeMetadata);

            if (bankTransfer != null && bankTransfer.Status != BankTransferStatus.Completed)
            {
                bankTransfer.MarkCompleted(DateTime.UtcNow, parsed.ProviderReference ?? attempt.RequestReference);

                _outboxService.Write(new BankTransferCompletedEvent(
                    TransferId: bankTransfer.Id,
                    TransactionReference: bankTransfer.Reference,
                    ProviderReference: parsed.ProviderReference ?? attempt.RequestReference,
                    OccurredOnUtc: DateTime.UtcNow));

                RecordAudit(AuditActions.BankTransferCompleted, AuditResourceTypes.BankTransfer, bankTransfer.Id.ToString(),
                    JsonSerializer.Serialize(new { bankTransfer.Reference, ProviderReference = parsed.ProviderReference }));
            }

            _outboxService.Write(new PaymentAttemptReconciledEvent(
                PaymentAttemptId: attempt.Id,
                LedgerTransactionId: attempt.LedgerTransactionId,
                Provider: provider,
                AttemptNumber: attempt.AttemptNumber,
                PreviousStatus: prevStatus,
                NewStatus: PaymentAttemptStatus.Succeeded,
                ProviderReference: parsed.ProviderReference,
                OccurredOnUtc: DateTime.UtcNow));

            var prevStatusStr = prevStatus.ToString();
            RecordAudit(AuditActions.PaymentAttemptReconciled, AuditResourceTypes.PaymentAttempt, attempt.Id.ToString(),
                JsonSerializer.Serialize(new { AttemptId = attempt.Id, PreviousStatus = prevStatusStr, NewStatus = "Succeeded" }));

            webhookEvent.MarkProcessed(attempt.Id, parsed.SafeMetadata);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            LogWebhookReconciledSuccess(_logger, attempt.Id, providerName);
            return WebhookProcessingResult.Processed(parsed.ProviderEventId, attempt.Id);
        }

        if (parsed.IsFailure)
        {
            // Provider confirms Failure
            var failCode = parsed.FailureCode ?? "GATEWAY_FAILED";
            var failReason = parsed.FailureReason ?? "Transfer rejected by gateway";
            attempt.MarkFailed(failCode, failReason, safeMetadata: parsed.SafeMetadata);

            if (bankTransfer != null && bankTransfer.Status != BankTransferStatus.Failed)
            {
                // Execute core financial reversal if still in PENDING / PROCESSING state
                await _ledgerPostingService.PostBankTransferReversalCoreAsync(bankTransfer.Id, failReason, cancellationToken).ConfigureAwait(false);

                _outboxService.Write(new BankTransferFailedEvent(
                    TransferId: bankTransfer.Id,
                    TransactionReference: bankTransfer.Reference,
                    Reason: failReason,
                    OccurredOnUtc: DateTime.UtcNow));

                RecordAudit(AuditActions.BankTransferReversed, AuditResourceTypes.BankTransfer, bankTransfer.Id.ToString(),
                    JsonSerializer.Serialize(new { bankTransfer.Reference, Reason = failReason }));
            }

            _outboxService.Write(new PaymentAttemptReconciledEvent(
                PaymentAttemptId: attempt.Id,
                LedgerTransactionId: attempt.LedgerTransactionId,
                Provider: provider,
                AttemptNumber: attempt.AttemptNumber,
                PreviousStatus: prevStatus,
                NewStatus: PaymentAttemptStatus.Failed,
                ProviderReference: parsed.ProviderReference,
                OccurredOnUtc: DateTime.UtcNow));

            var prevStatusStr = prevStatus.ToString();
            RecordAudit(AuditActions.PaymentAttemptReconciled, AuditResourceTypes.PaymentAttempt, attempt.Id.ToString(),
                JsonSerializer.Serialize(new { AttemptId = attempt.Id, PreviousStatus = prevStatusStr, NewStatus = "Failed", failReason }));

            webhookEvent.MarkProcessed(attempt.Id, parsed.SafeMetadata);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            LogWebhookReconciledFailure(_logger, attempt.Id, providerName, failReason);
            return WebhookProcessingResult.Processed(parsed.ProviderEventId, attempt.Id);
        }

        // In-flight / informational event
        webhookEvent.MarkProcessed(attempt.Id, parsed.SafeMetadata);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return WebhookProcessingResult.Processed(parsed.ProviderEventId, attempt.Id, "Informational webhook recorded.");
    }

    private void RecordAudit(string action, string resourceType, string resourceId, string detailsJson)
    {
        var audit = AuditLog.Create(
            actorId: "SYSTEM",
            action: action,
            resourceType: resourceType,
            resourceId: resourceId,
            afterJson: detailsJson);

        _dbContext.AuditLogs.Add(audit);
    }

    private static string ComputePayloadHash(string rawPayload)
    {
        var bytes = Encoding.UTF8.GetBytes(rawPayload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    private static ParsedWebhookPayload? ParsePayload(PaymentProvider provider, string rawPayload)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;

            return provider switch
            {
                PaymentProvider.Flutterwave => ParseFlutterwave(root, rawPayload),
                PaymentProvider.Paystack => ParsePaystack(root, rawPayload),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static ParsedWebhookPayload? ParseFlutterwave(JsonElement root, string rawPayload)
    {
        var eventType = root.TryGetProperty("event", out var ev) ? ev.GetString() : "transfer.completed";
        if (string.IsNullOrWhiteSpace(eventType)) eventType = "transfer.completed";

        if (!root.TryGetProperty("data", out var data))
            return null;

        long id = 0;
        if (data.TryGetProperty("id", out var idProp))
        {
            if (idProp.ValueKind == JsonValueKind.Number) id = idProp.GetInt64();
            else if (idProp.ValueKind == JsonValueKind.String && long.TryParse(idProp.GetString(), CultureInfo.InvariantCulture, out var parsedId)) id = parsedId;
        }

        var status = data.TryGetProperty("status", out var st) ? st.GetString()?.ToUpperInvariant() : null;
        var reference = data.TryGetProperty("reference", out var rf) ? rf.GetString() : null;
        var currency = data.TryGetProperty("currency", out var curr) ? curr.GetString() : null;
        decimal? amount = data.TryGetProperty("amount", out var am) && am.ValueKind == JsonValueKind.Number ? am.GetDecimal() : null;
        var completeMessage = data.TryGetProperty("complete_message", out var cm) ? cm.GetString() : null;

        var providerEventId = id > 0
            ? string.Format(CultureInfo.InvariantCulture, "flw_evt_{0}_{1}", id, status ?? "unknown")
            : ComputePayloadHash(rawPayload);

        var isSuccess = status == "SUCCESSFUL";
        var isFailure = status == "FAILED";

        var safeMeta = JsonSerializer.Serialize(new
        {
            provider_id = id,
            status = status,
            reference = reference
        });

        return new ParsedWebhookPayload(
            ProviderEventId: providerEventId,
            EventType: eventType,
            Reference: reference,
            ProviderReference: id > 0 ? id.ToString(CultureInfo.InvariantCulture) : reference,
            Amount: amount,
            Currency: currency,
            ProviderStatus: status,
            IsSuccess: isSuccess,
            IsFailure: isFailure,
            FailureCode: isFailure ? "TRANSFER_FAILED" : null,
            FailureReason: completeMessage ?? (isFailure ? "Transfer rejected by gateway" : null),
            SafeMetadata: safeMeta);
    }

    private static ParsedWebhookPayload? ParsePaystack(JsonElement root, string rawPayload)
    {
        var eventType = root.TryGetProperty("event", out var ev) ? ev.GetString() : "transfer.success";
        if (string.IsNullOrWhiteSpace(eventType)) eventType = "transfer.unknown";

        if (!root.TryGetProperty("data", out var data))
            return null;

        var transferCode = data.TryGetProperty("transfer_code", out var tc) ? tc.GetString() : null;
        var reference = data.TryGetProperty("reference", out var rf) ? rf.GetString() : null;
        var status = data.TryGetProperty("status", out var st) ? st.GetString()?.ToLowerInvariant() : null;
        var currency = data.TryGetProperty("currency", out var curr) ? curr.GetString() : null;

        decimal? amount = null;
        if (data.TryGetProperty("amount", out var am) && am.ValueKind == JsonValueKind.Number)
        {
            var rawAmount = am.GetDecimal();
            amount = string.Equals(currency, "NGN", StringComparison.OrdinalIgnoreCase)
                ? rawAmount / 100m
                : rawAmount;
        }

        var isSuccess = status == "success" || eventType.EndsWith(".success", StringComparison.OrdinalIgnoreCase);
        var isFailure = status == "failed" || status == "reversed" || eventType.EndsWith(".failed", StringComparison.OrdinalIgnoreCase) || eventType.EndsWith(".reversed", StringComparison.OrdinalIgnoreCase);

        var providerEventId = !string.IsNullOrWhiteSpace(transferCode)
            ? string.Format(CultureInfo.InvariantCulture, "pstk_evt_{0}_{1}_{2}", eventType, transferCode, status ?? "unknown")
            : ComputePayloadHash(rawPayload);

        var safeMeta = JsonSerializer.Serialize(new
        {
            transfer_code = transferCode,
            reference = reference,
            status = status,
            event_type = eventType
        });

        return new ParsedWebhookPayload(
            ProviderEventId: providerEventId,
            EventType: eventType,
            Reference: reference,
            ProviderReference: transferCode ?? reference,
            Amount: amount,
            Currency: currency,
            ProviderStatus: status,
            IsSuccess: isSuccess,
            IsFailure: isFailure,
            FailureCode: isFailure ? "TRANSFER_FAILED" : null,
            FailureReason: isFailure ? string.Format(CultureInfo.InvariantCulture, "Transfer status is '{0}'", status) : null,
            SafeMetadata: safeMeta);
    }

    private sealed record ParsedWebhookPayload(
        string ProviderEventId,
        string EventType,
        string? Reference,
        string? ProviderReference,
        decimal? Amount,
        string? Currency,
        string? ProviderStatus,
        bool IsSuccess,
        bool IsFailure,
        string? FailureCode,
        string? FailureReason,
        string? SafeMetadata);

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Webhook signature verification failed for provider {Provider}")]
    private static partial void LogWebhookSignatureFailed(ILogger logger, string provider);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Malformed webhook payload for provider {Provider}")]
    private static partial void LogWebhookMalformedPayload(ILogger logger, string provider);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Duplicate webhook received from {Provider}, EventId: {EventId}. Safely acknowledged.")]
    private static partial void LogWebhookDuplicate(ILogger logger, string provider, string eventId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "PaymentAttempt not found for webhook from {Provider}, EventId: {EventId}, Reference: {Reference}")]
    private static partial void LogWebhookAttemptNotFound(ILogger logger, string provider, string eventId, string reference);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Webhook amount mismatch for PaymentAttempt {AttemptId}. Attempt: {AttemptAmount}, Webhook: {WebhookAmount}")]
    private static partial void LogWebhookAmountMismatch(ILogger logger, Guid attemptId, decimal attemptAmount, decimal webhookAmount);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Webhook currency mismatch for PaymentAttempt {AttemptId}. Attempt: {AttemptCurrency}, Webhook: {WebhookCurrency}")]
    private static partial void LogWebhookCurrencyMismatch(ILogger logger, Guid attemptId, string attemptCurrency, string webhookCurrency);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "PaymentAttempt {AttemptId} successfully reconciled to SUCCEEDED via webhook from {Provider}")]
    private static partial void LogWebhookReconciledSuccess(ILogger logger, Guid attemptId, string provider);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "PaymentAttempt {AttemptId} reconciled to FAILED via webhook from {Provider}. Reason: {Reason}")]
    private static partial void LogWebhookReconciledFailure(ILogger logger, Guid attemptId, string provider, string reason);

    [LoggerMessage(EventId = 9, Level = LogLevel.Information, Message = "Stale out-of-order webhook ignored for PaymentAttempt {AttemptId}. Current: {CurrentStatus}, Received: {ReceivedStatus}")]
    private static partial void LogWebhookStaleIgnored(ILogger logger, Guid attemptId, string currentStatus, string? receivedStatus);
}
