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
using CebizPay.Infrastructure.Payments.Monnify;
using CebizPay.Infrastructure.Payments.Paystack;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Payments.Common;

/// <summary>
/// Infrastructure implementation of <see cref="IWebhookProcessor"/> coordinating secure ingestion,
/// deduplication, state transition validation, and financial reconciliation for external payment webhooks
/// (including outbound payout reconciliation, inbound virtual account deposits, and card funding).
/// </summary>
public sealed partial class WebhookProcessor : IWebhookProcessor
{
    private readonly IWebhookSignatureVerifier _signatureVerifier;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILedgerPostingService _ledgerPostingService;
    private readonly IPlatformFeePolicyService _feePolicyService;
    private readonly IOutboxService _outboxService;
    private readonly FlutterwaveOptions _flwOptions;
    private readonly PaystackOptions _pstkOptions;
    private readonly MonnifyOptions _monnifyOptions;
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
        IPlatformFeePolicyService feePolicyService,
        IOutboxService outboxService,
        IOptions<FlutterwaveOptions> flwOptions,
        IOptions<PaystackOptions> pstkOptions,
        IOptions<MonnifyOptions> monnifyOptions,
        ILogger<WebhookProcessor> logger)
    {
        _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _ledgerPostingService = ledgerPostingService ?? throw new ArgumentNullException(nameof(ledgerPostingService));
        _feePolicyService = feePolicyService ?? throw new ArgumentNullException(nameof(feePolicyService));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _flwOptions = flwOptions?.Value ?? throw new ArgumentNullException(nameof(flwOptions));
        _pstkOptions = pstkOptions?.Value ?? throw new ArgumentNullException(nameof(pstkOptions));
        _monnifyOptions = monnifyOptions?.Value ?? throw new ArgumentNullException(nameof(monnifyOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    public async Task<WebhookProcessingResult> IngestWebhookAsync(
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
            PaymentProvider.Monnify => !string.IsNullOrWhiteSpace(_monnifyOptions.WebhookSecret)
                ? _monnifyOptions.WebhookSecret
                : _monnifyOptions.SecretKey,
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
            if (existingEvent.Status == WebhookEventStatus.Failed || existingEvent.Status == WebhookEventStatus.DeadLetter)
            {
                existingEvent.ReleaseClaim("Re-triggered via duplicate delivery for previously failed event", TimeSpan.Zero);
                RecordAudit(AuditActions.WebhookReactivated, AuditResourceTypes.WebhookEvent, existingEvent.Id.ToString(),
                    JsonSerializer.Serialize(new { Provider = providerName, ProviderEventId = parsed.ProviderEventId, PreviousStatus = existingEvent.Status.ToString() }));
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return WebhookProcessingResult.Processed(parsed.ProviderEventId);
            }

            LogWebhookDuplicate(_logger, providerName, parsed.ProviderEventId);
            RecordAudit(AuditActions.WebhookDuplicate, AuditResourceTypes.WebhookEvent, existingEvent.Id.ToString(),
                JsonSerializer.Serialize(new { Provider = providerName, ProviderEventId = parsed.ProviderEventId }));
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return WebhookProcessingResult.Duplicate(parsed.ProviderEventId);
        }

        // Persist initial durable WebhookEvent in RECEIVED status with safe metadata
        var safeMetadataJson = JsonSerializer.Serialize(parsed, JsonOptions);
        var correlationRef = parsed.Reference ?? parsed.AccountNumber;

        var webhookEvent = WebhookEvent.Create(
            provider: provider,
            providerEventId: parsed.ProviderEventId,
            eventType: parsed.EventType,
            payloadHash: payloadHash,
            safeMetadata: safeMetadataJson,
            correlationReference: correlationRef);

        _dbContext.WebhookEvents.Add(webhookEvent);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return WebhookProcessingResult.Processed(parsed.ProviderEventId);
    }

    /// <inheritdoc/>
    public async Task<WebhookProcessingResult> ProcessFinancialWebhookEventAsync(
        Guid webhookEventId,
        CancellationToken cancellationToken = default)
    {
        WebhookEvent? webhookEvent;
        if (_dbContext.Database.IsNpgsql())
        {
            webhookEvent = await _dbContext.WebhookEvents
                .FromSqlRaw("SELECT * FROM \"WebhookEvents\" WHERE \"Id\" = {0} FOR UPDATE", webhookEventId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            webhookEvent = await _dbContext.WebhookEvents
                .FirstOrDefaultAsync(w => w.Id == webhookEventId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (webhookEvent == null)
        {
            return WebhookProcessingResult.Error(webhookEventId.ToString(), "WebhookEvent not found.");
        }

        if (webhookEvent.Status == WebhookEventStatus.Processed)
        {
            return WebhookProcessingResult.Processed(webhookEvent.ProviderEventId, webhookEvent.PaymentAttemptId, "Already processed.");
        }

        ParsedWebhookPayload? parsed = null;
        if (!string.IsNullOrWhiteSpace(webhookEvent.SafeMetadata))
        {
            try
            {
                parsed = JsonSerializer.Deserialize<ParsedWebhookPayload>(webhookEvent.SafeMetadata, JsonOptions);
            }
            catch
            {
                // Fallback to minimal payload
            }
        }

        parsed ??= new ParsedWebhookPayload(
            ProviderEventId: webhookEvent.ProviderEventId,
            EventType: webhookEvent.EventType,
            Reference: webhookEvent.CorrelationReference,
            ProviderReference: null,
            AccountNumber: webhookEvent.CorrelationReference,
            Amount: null,
            Currency: null,
            ProviderStatus: null,
            IsSuccess: false,
            IsFailure: false,
            FailureCode: null,
            FailureReason: null,
            SafeMetadata: null);

        var provider = webhookEvent.Provider;
        var providerName = provider.ToString();

        // Resolve target: Outbound PaymentAttempt, Inbound ExternalFundingAccount, Inbound VirtualAccount, or Card Funding
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

        // --- Handle Outbound PaymentAttempt ---
        if (attempt != null)
        {
            return await ProcessOutboundAttemptWebhookAsync(provider, parsed, attempt, webhookEvent, cancellationToken).ConfigureAwait(false);
        }

        // --- Handle Inbound ExternalFundingAccount Deposit (e.g. Monnify Reserved Virtual Account) ---
        ExternalFundingAccount? externalFundingAccount = null;
        if (!string.IsNullOrWhiteSpace(parsed.AccountNumber))
        {
            externalFundingAccount = await _dbContext.ExternalFundingAccounts
                .Include(e => e.Wallet)
                .FirstOrDefaultAsync(e => e.Provider == provider && e.AccountNumber == parsed.AccountNumber, cancellationToken)
                .ConfigureAwait(false);
        }

        if (externalFundingAccount != null)
        {
            return await ProcessExternalFundingAccountDepositAsync(provider, parsed, externalFundingAccount, webhookEvent, cancellationToken).ConfigureAwait(false);
        }

        // --- Handle Inbound Legacy Virtual Account Deposit (DVA) ---
        VirtualAccount? virtualAccount = null;
        if (!string.IsNullOrWhiteSpace(parsed.AccountNumber))
        {
            virtualAccount = await _dbContext.VirtualAccounts
                .FirstOrDefaultAsync(v => v.Provider == provider && v.AccountNumber == parsed.AccountNumber, cancellationToken)
                .ConfigureAwait(false);
        }

        if (virtualAccount != null)
        {
            return await ProcessInboundVirtualAccountDepositAsync(provider, parsed, virtualAccount, webhookEvent, cancellationToken).ConfigureAwait(false);
        }

        // --- Handle Inbound Card Funding Payment ---
        FundingTransaction? fundingTx = null;
        if (!string.IsNullOrWhiteSpace(parsed.Reference))
        {
            fundingTx = await _dbContext.FundingTransactions
                .FirstOrDefaultAsync(f => f.Provider == provider && f.ProviderTransactionReference == parsed.Reference, cancellationToken)
                .ConfigureAwait(false);
        }

        if (fundingTx != null)
        {
            return await ProcessCardFundingWebhookAsync(provider, parsed, fundingTx, webhookEvent, cancellationToken).ConfigureAwait(false);
        }

        // Unresolved reference / Unmatched account
        var parsedRef = parsed.Reference ?? parsed.AccountNumber ?? string.Empty;
        LogWebhookAttemptNotFound(_logger, providerName, parsed.ProviderEventId, parsedRef);
        RecordAudit(AuditActions.WebhookUnmatchedTransaction, AuditResourceTypes.WebhookEvent, webhookEvent.Id.ToString(),
            JsonSerializer.Serialize(new { Provider = providerName, ProviderEventId = parsed.ProviderEventId, Reference = parsedRef, AccountNumber = parsed.AccountNumber }));
        webhookEvent.MarkIgnored("No matching PaymentAttempt, ExternalFundingAccount, VirtualAccount, or FundingTransaction found for reference.");
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return WebhookProcessingResult.Ignored(parsed.ProviderEventId, "No matching PaymentAttempt, ExternalFundingAccount, VirtualAccount, or FundingTransaction found for reference.");
    }

    /// <inheritdoc/>
    public async Task<WebhookProcessingResult> ProcessWebhookAsync(
        PaymentProvider provider,
        string rawPayload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        var ingestResult = await IngestWebhookAsync(provider, rawPayload, headers, cancellationToken).ConfigureAwait(false);
        if (ingestResult.Status != WebhookProcessingStatus.Processed)
        {
            return ingestResult;
        }

        var webhookEvent = await _dbContext.WebhookEvents
            .FirstOrDefaultAsync(w => w.Provider == provider && w.ProviderEventId == ingestResult.ProviderEventId, cancellationToken)
            .ConfigureAwait(false);

        if (webhookEvent == null)
        {
            return ingestResult;
        }

        return await ProcessFinancialWebhookEventAsync(webhookEvent.Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WebhookProcessingResult> ProcessOutboundAttemptWebhookAsync(
        PaymentProvider provider,
        ParsedWebhookPayload parsed,
        PaymentAttempt attempt,
        WebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        var providerName = provider.ToString();

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
            var failCode = parsed.FailureCode ?? "GATEWAY_FAILED";
            var failReason = parsed.FailureReason ?? "Transfer rejected by gateway";
            attempt.MarkFailed(failCode, failReason, safeMetadata: parsed.SafeMetadata);

            if (bankTransfer != null && bankTransfer.Status != BankTransferStatus.Failed)
            {
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

        webhookEvent.MarkProcessed(attempt.Id, parsed.SafeMetadata);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return WebhookProcessingResult.Processed(parsed.ProviderEventId, attempt.Id, "Informational webhook recorded.");
    }

    private async Task<WebhookProcessingResult> ProcessExternalFundingAccountDepositAsync(
        PaymentProvider provider,
        ParsedWebhookPayload parsed,
        ExternalFundingAccount externalFundingAccount,
        WebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        if (externalFundingAccount.Status != ExternalFundingAccountStatus.Active)
        {
            var errorMsg = $"External funding account '{externalFundingAccount.AccountNumber}' is not active (status: {externalFundingAccount.Status}).";
            webhookEvent.MarkFailed(errorMsg);
            RecordAudit(AuditActions.WebhookRejected, AuditResourceTypes.ExternalFundingAccount, externalFundingAccount.Id.ToString(),
                JsonSerializer.Serialize(new { AccountNumber = externalFundingAccount.AccountNumber, Reason = errorMsg }));
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return WebhookProcessingResult.Error(parsed.ProviderEventId, errorMsg);
        }

        var wallet = externalFundingAccount.Wallet
            ?? await _dbContext.Wallets.FirstOrDefaultAsync(w => w.Id == externalFundingAccount.WalletId, cancellationToken).ConfigureAwait(false);

        if (wallet == null || wallet.Status != WalletStatus.Active)
        {
            var errorMsg = "Recipient wallet for external funding account not found or is not active.";
            webhookEvent.MarkFailed(errorMsg);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return WebhookProcessingResult.Error(parsed.ProviderEventId, errorMsg);
        }

        if (!parsed.Amount.HasValue || parsed.Amount.Value <= 0)
        {
            var errorMsg = "Funding deposit amount is missing or non-positive.";
            webhookEvent.MarkFailed(errorMsg);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return WebhookProcessingResult.Error(parsed.ProviderEventId, errorMsg);
        }

        // Currency validation
        Currency depositCurrency;
        if (!string.IsNullOrWhiteSpace(parsed.Currency) && Enum.TryParse<Currency>(parsed.Currency, true, out var parsedCurrency))
        {
            depositCurrency = parsedCurrency;
        }
        else
        {
            depositCurrency = externalFundingAccount.Currency;
        }

        if (depositCurrency != externalFundingAccount.Currency || depositCurrency != wallet.Currency)
        {
            var errorMsg = $"Currency mismatch. Deposit: {depositCurrency}, Account: {externalFundingAccount.Currency}, Wallet: {wallet.Currency}";
            webhookEvent.MarkFailed(errorMsg);
            RecordAudit(AuditActions.WebhookRejected, AuditResourceTypes.ExternalFundingAccount, externalFundingAccount.Id.ToString(),
                JsonSerializer.Serialize(new { AccountNumber = externalFundingAccount.AccountNumber, Reason = errorMsg }));
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return WebhookProcessingResult.Error(parsed.ProviderEventId, errorMsg);
        }

        if (!parsed.IsSuccess)
        {
            var errorMsg = $"Provider indicated non-successful status: {parsed.ProviderStatus}";
            webhookEvent.MarkFailed(errorMsg);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return WebhookProcessingResult.Error(parsed.ProviderEventId, errorMsg);
        }

        var depositReference = parsed.ProviderReference ?? parsed.Reference ?? parsed.ProviderEventId;

        // Calculate platform fee
        var feePolicy = await _feePolicyService.GetActivePolicyAsync(FeeOperationType.VirtualAccountFunding, cancellationToken).ConfigureAwait(false);
        decimal feeAmount = 0m;
        decimal netCreditedAmount = parsed.Amount.Value;
        Guid? feePolicyId = null;
        int? feePolicyVersion = null;
        FeeBearer? feeBearer = FeeBearer.CustomerPays;

        if (feePolicy != null)
        {
            var breakdown = feePolicy.CalculateBreakdown(parsed.Amount.Value, depositCurrency);
            feeAmount = breakdown.Fee;
            netCreditedAmount = breakdown.NetBeneficiaryCredit;
            feePolicyId = feePolicy.Id;
            feePolicyVersion = feePolicy.Version;
            feeBearer = feePolicy.FeeBearer;
        }

        // Post inbound double-entry credit through central ledger
        await using var dbTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var (txn, funding) = await _ledgerPostingService.PostExternalFundingAccountCreditCoreAsync(
                walletId: wallet.Id,
                externalFundingAccountId: externalFundingAccount.Id,
                grossAmount: parsed.Amount.Value,
                feeAmount: feeAmount,
                netCreditedAmount: netCreditedAmount,
                providerFeeAmount: 0m,
                currency: depositCurrency,
                provider: provider,
                providerTransactionReference: depositReference,
                providerEventReference: parsed.ProviderEventId,
                feePolicyId: feePolicyId,
                feePolicyVersion: feePolicyVersion,
                feeBearer: feeBearer,
                channel: FundingChannel.VirtualAccount,
                description: $"Inbound deposit via {provider} account {externalFundingAccount.AccountNumber}",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            RecordAudit(AuditActions.FundingReceived, AuditResourceTypes.FundingTransaction, funding.Id.ToString(),
                JsonSerializer.Serialize(new
                {
                    AccountNumber = externalFundingAccount.AccountNumber,
                    GrossAmount = parsed.Amount.Value,
                    FeeAmount = feeAmount,
                    NetCreditedAmount = netCreditedAmount,
                    Currency = depositCurrency.ToString(),
                    Reference = depositReference
                }));

            RecordAudit(AuditActions.PaymentFundingCompleted, AuditResourceTypes.FundingTransaction, funding.Id.ToString(),
                JsonSerializer.Serialize(new
                {
                    FundingTransactionId = funding.Id,
                    LedgerTransactionId = txn.Id,
                    WalletId = wallet.Id,
                    NetCreditedAmount = netCreditedAmount
                }));

            _outboxService.Write(new ExternalFundingAccountDepositCompletedDomainEvent(
                FundingTransactionId: funding.Id,
                WalletId: wallet.Id,
                ExternalFundingAccountId: externalFundingAccount.Id,
                LedgerTransactionId: txn.Id,
                GrossAmount: parsed.Amount.Value,
                FeeAmount: feeAmount,
                NetCreditedAmount: netCreditedAmount,
                Currency: depositCurrency,
                Provider: provider,
                ProviderTransactionReference: depositReference,
                OccurredOnUtc: DateTime.UtcNow));

            webhookEvent.MarkProcessed(null, parsed.SafeMetadata);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await dbTx.CommitAsync(cancellationToken).ConfigureAwait(false);

            var currStr = depositCurrency.ToString();
            LogVirtualAccountDepositSuccess(_logger, netCreditedAmount, currStr, wallet.Id);
            return WebhookProcessingResult.Processed(parsed.ProviderEventId, null, "External funding account deposit credited.");
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            LogVirtualAccountDepositException(_logger, parsed.ProviderEventId, ex);
            webhookEvent.ReleaseClaim($"Credit failure: {ex.Message}", TimeSpan.FromSeconds(10));
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return WebhookProcessingResult.Error(parsed.ProviderEventId, ex.Message);
        }
    }

    private async Task<WebhookProcessingResult> ProcessInboundVirtualAccountDepositAsync(
        PaymentProvider provider,
        ParsedWebhookPayload parsed,
        VirtualAccount virtualAccount,
        WebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        if (!parsed.IsSuccess || !parsed.Amount.HasValue || parsed.Amount.Value <= 0)
        {
            webhookEvent.MarkProcessed(null, parsed.SafeMetadata);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return WebhookProcessingResult.Processed(parsed.ProviderEventId, null, "Non-credit virtual account event recorded.");
        }

        // Resolve recipient wallet
        var walletQuery = _dbContext.Wallets.AsQueryable();
        if (!string.IsNullOrWhiteSpace(virtualAccount.IndividualId))
        {
            walletQuery = walletQuery.Where(w => w.IndividualId == virtualAccount.IndividualId && w.Currency == virtualAccount.Currency);
        }
        else if (virtualAccount.OrganizationId.HasValue)
        {
            walletQuery = walletQuery.Where(w => w.OrganizationId == virtualAccount.OrganizationId.Value && w.Currency == virtualAccount.Currency);
        }

        var wallet = await walletQuery.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (wallet == null)
        {
            webhookEvent.MarkFailed("Recipient wallet for virtual account not found.");
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return WebhookProcessingResult.Error(parsed.ProviderEventId, "Recipient wallet not found.");
        }

        var depositReference = parsed.ProviderReference ?? parsed.Reference ?? parsed.ProviderEventId;

        // Post inbound double-entry credit through central ledger
        await using var dbTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var (txn, funding) = await _ledgerPostingService.PostInboundFundingCreditCoreAsync(
                walletId: wallet.Id,
                virtualAccountId: virtualAccount.Id,
                amount: parsed.Amount.Value,
                currency: virtualAccount.Currency,
                provider: provider,
                providerTransactionReference: depositReference,
                channel: FundingChannel.VirtualAccount,
                description: $"Inbound deposit via virtual account {virtualAccount.AccountNumber}",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            RecordAudit(AuditActions.FundingReceived, AuditResourceTypes.FundingTransaction, funding.Id.ToString(),
                JsonSerializer.Serialize(new { AccountNumber = virtualAccount.AccountNumber, Amount = parsed.Amount.Value, virtualAccount.Currency, Reference = depositReference }));

            webhookEvent.MarkProcessed(null, parsed.SafeMetadata);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await dbTx.CommitAsync(cancellationToken).ConfigureAwait(false);

            var currStr = virtualAccount.Currency.ToString();
            LogVirtualAccountDepositSuccess(_logger, parsed.Amount.Value, currStr, wallet.Id);
            return WebhookProcessingResult.Processed(parsed.ProviderEventId, null, "Virtual account deposit credited.");
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            LogVirtualAccountDepositException(_logger, parsed.ProviderEventId, ex);
            webhookEvent.ReleaseClaim($"Credit failure: {ex.Message}", TimeSpan.FromSeconds(10));
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return WebhookProcessingResult.Error(parsed.ProviderEventId, ex.Message);
        }
    }

    private async Task<WebhookProcessingResult> ProcessCardFundingWebhookAsync(
        PaymentProvider provider,
        ParsedWebhookPayload parsed,
        FundingTransaction fundingTx,
        WebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        if (fundingTx.Status == FundingTransactionStatus.Completed)
        {
            webhookEvent.MarkProcessed(null, parsed.SafeMetadata);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return WebhookProcessingResult.Processed(parsed.ProviderEventId, null, "Funding transaction already completed.");
        }

        if (parsed.IsSuccess)
        {
            // Amount & currency verification
            if (parsed.Amount.HasValue && parsed.Amount.Value != fundingTx.Amount)
            {
                var errorMsg = $"Card funding amount mismatch. Expected: {fundingTx.Amount}, Received: {parsed.Amount.Value}";
                webhookEvent.MarkFailed(errorMsg);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return WebhookProcessingResult.Error(parsed.ProviderEventId, errorMsg);
            }

            // Calculate platform fee breakdown
            var feePolicy = await _feePolicyService.GetActivePolicyAsync(FeeOperationType.CardFunding, cancellationToken).ConfigureAwait(false);
            decimal feeAmount = 0m;
            decimal netCreditedAmount = fundingTx.Amount;
            Guid? feePolicyId = null;
            int? feePolicyVersion = null;
            FeeBearer? feeBearer = FeeBearer.CustomerPays;

            if (feePolicy != null)
            {
                var breakdown = feePolicy.CalculateBreakdown(fundingTx.Amount, fundingTx.Currency);
                feeAmount = breakdown.Fee;
                netCreditedAmount = breakdown.NetBeneficiaryCredit;
                feePolicyId = feePolicy.Id;
                feePolicyVersion = feePolicy.Version;
                feeBearer = feePolicy.FeeBearer;
            }

            await using var dbTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var (txn, funding) = await _ledgerPostingService.PostCardFundingCreditCoreAsync(
                    walletId: fundingTx.WalletId,
                    grossAmount: fundingTx.Amount,
                    feeAmount: feeAmount,
                    netCreditedAmount: netCreditedAmount,
                    providerFeeAmount: 0m,
                    currency: fundingTx.Currency,
                    provider: provider,
                    providerTransactionReference: fundingTx.ProviderTransactionReference,
                    providerEventReference: parsed.ProviderEventId,
                    feePolicyId: feePolicyId,
                    feePolicyVersion: feePolicyVersion,
                    feeBearer: feeBearer,
                    description: $"Card deposit via {provider} ({fundingTx.ProviderTransactionReference})",
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                RecordAudit(AuditActions.CardFundingCompleted, AuditResourceTypes.FundingTransaction, funding.Id.ToString(),
                    JsonSerializer.Serialize(new
                    {
                        fundingTx.ProviderTransactionReference,
                        GrossAmount = fundingTx.Amount,
                        FeeAmount = feeAmount,
                        NetCreditedAmount = netCreditedAmount,
                        Currency = fundingTx.Currency.ToString()
                    }));

                _outboxService.Write(new CardFundingCompletedDomainEvent(
                    FundingTransactionId: funding.Id,
                    WalletId: fundingTx.WalletId,
                    LedgerTransactionId: txn.Id,
                    Amount: fundingTx.Amount,
                    Currency: fundingTx.Currency,
                    Provider: provider,
                    ProviderTransactionReference: fundingTx.ProviderTransactionReference,
                    OccurredOnUtc: DateTime.UtcNow));

                webhookEvent.MarkProcessed(null, parsed.SafeMetadata);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await dbTx.CommitAsync(cancellationToken).ConfigureAwait(false);

                var currStr = fundingTx.Currency.ToString();
                LogCardFundingSuccess(_logger, fundingTx.ProviderTransactionReference, fundingTx.Amount, currStr, fundingTx.WalletId);
                return WebhookProcessingResult.Processed(parsed.ProviderEventId, null, "Card funding credited.");
            }
            catch (Exception ex)
            {
                await dbTx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                LogCardFundingException(_logger, fundingTx.ProviderTransactionReference, ex);
                webhookEvent.ReleaseClaim($"Credit failure: {ex.Message}", TimeSpan.FromSeconds(10));
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return WebhookProcessingResult.Error(parsed.ProviderEventId, ex.Message);
            }
        }

        if (parsed.IsFailure)
        {
            fundingTx.MarkFailed(parsed.FailureReason ?? "Card payment failed at gateway");
            RecordAudit(AuditActions.CardFundingFailed, AuditResourceTypes.FundingTransaction, fundingTx.Id.ToString(),
                JsonSerializer.Serialize(new { fundingTx.ProviderTransactionReference, Reason = parsed.FailureReason }));

            webhookEvent.MarkProcessed(null, parsed.SafeMetadata);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return WebhookProcessingResult.Processed(parsed.ProviderEventId, null, "Card funding failure recorded.");
        }

        webhookEvent.MarkProcessed(null, parsed.SafeMetadata);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return WebhookProcessingResult.Processed(parsed.ProviderEventId, null, "Informational card funding event recorded.");
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
                PaymentProvider.Monnify => ParseMonnify(root, rawPayload),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static ParsedWebhookPayload? ParseMonnify(JsonElement root, string rawPayload)
    {
        var eventType = root.TryGetProperty("eventType", out var ev) ? ev.GetString() : "SUCCESSFUL_TRANSACTION";
        if (string.IsNullOrWhiteSpace(eventType)) eventType = "SUCCESSFUL_TRANSACTION";

        if (!root.TryGetProperty("eventData", out var data))
            return null;

        var txRef = data.TryGetProperty("transactionReference", out var tr) ? tr.GetString() : null;
        var payRef = data.TryGetProperty("paymentReference", out var pr) ? pr.GetString() : null;
        var refProp = data.TryGetProperty("reference", out var rf) ? rf.GetString() : null;
        var effectiveRef = refProp ?? txRef ?? payRef;

        var statusProp = data.TryGetProperty("status", out var st) ? st.GetString()?.ToUpperInvariant() : null;
        var paymentStatus = data.TryGetProperty("paymentStatus", out var ps) ? ps.GetString()?.ToUpperInvariant() : statusProp;
        var currency = data.TryGetProperty("currency", out var curr)
            ? curr.GetString()
            : data.TryGetProperty("currencyCode", out var cc) ? cc.GetString() : "NGN";

        string? accountNumber = null;
        string? bankCode = null;
        if (data.TryGetProperty("destinationAccountInformation", out var destInfo))
        {
            if (destInfo.TryGetProperty("accountNumber", out var an)) accountNumber = an.GetString();
            if (destInfo.TryGetProperty("bankCode", out var bc)) bankCode = bc.GetString();
        }
        else if (data.TryGetProperty("destinationAccountNumber", out var dan))
        {
            accountNumber = dan.GetString();
            if (data.TryGetProperty("destinationBankCode", out var dbc)) bankCode = dbc.GetString();
        }

        decimal? amount = null;
        if (data.TryGetProperty("amountPaid", out var am) && am.ValueKind == JsonValueKind.Number)
        {
            amount = am.GetDecimal();
        }
        else if (data.TryGetProperty("amount", out var amt) && amt.ValueKind == JsonValueKind.Number)
        {
            amount = amt.GetDecimal();
        }
        else if (data.TryGetProperty("totalPayable", out var tp) && tp.ValueKind == JsonValueKind.Number)
        {
            amount = tp.GetDecimal();
        }

        var isSuccess = paymentStatus is "PAID" or "SUCCESS" or "SUCCESSFUL" || eventType.Contains("SUCCESSFUL", StringComparison.OrdinalIgnoreCase);
        var isFailure = paymentStatus is "FAILED" or "EXPIRED" or "CANCELLED" or "REVERSED" || eventType.Contains("FAILED", StringComparison.OrdinalIgnoreCase) || eventType.Contains("REVERSED", StringComparison.OrdinalIgnoreCase);

        var providerEventId = !string.IsNullOrWhiteSpace(txRef)
            ? string.Format(CultureInfo.InvariantCulture, "mnfy_evt_{0}_{1}_{2}", eventType, txRef, paymentStatus ?? "unknown")
            : !string.IsNullOrWhiteSpace(effectiveRef)
                ? string.Format(CultureInfo.InvariantCulture, "mnfy_evt_{0}_{1}_{2}", eventType, effectiveRef, paymentStatus ?? "unknown")
                : ComputePayloadHash(rawPayload);

        var safeMeta = JsonSerializer.Serialize(new
        {
            transaction_reference = txRef,
            payment_reference = payRef,
            reference = refProp,
            payment_status = paymentStatus,
            event_type = eventType,
            account_number = accountNumber,
            bank_code = bankCode
        });

        return new ParsedWebhookPayload(
            ProviderEventId: providerEventId,
            EventType: eventType,
            Reference: effectiveRef,
            ProviderReference: txRef ?? payRef ?? refProp,
            AccountNumber: accountNumber,
            Amount: amount,
            Currency: currency,
            ProviderStatus: paymentStatus,
            IsSuccess: isSuccess,
            IsFailure: isFailure,
            FailureCode: isFailure ? "DISBURSEMENT_FAILED" : null,
            FailureReason: isFailure ? "Monnify reported disbursement as failed or reversed" : null,
            SafeMetadata: safeMeta);
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
        var txRef = data.TryGetProperty("tx_ref", out var tr) ? tr.GetString() : null;
        var effectiveRef = reference ?? txRef;

        var accountNumber = data.TryGetProperty("account_number", out var an) ? an.GetString() : null;
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
            reference = effectiveRef,
            account_number = accountNumber
        });

        return new ParsedWebhookPayload(
            ProviderEventId: providerEventId,
            EventType: eventType,
            Reference: effectiveRef,
            ProviderReference: id > 0 ? id.ToString(CultureInfo.InvariantCulture) : effectiveRef,
            AccountNumber: accountNumber,
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

        string? accountNumber = null;
        if (data.TryGetProperty("authorization", out var auth) && auth.TryGetProperty("account_number", out var an))
        {
            accountNumber = an.GetString();
        }
        else if (data.TryGetProperty("dedicated_account", out var da) && da.TryGetProperty("account_number", out var daAn))
        {
            accountNumber = daAn.GetString();
        }

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
            : !string.IsNullOrWhiteSpace(reference)
                ? string.Format(CultureInfo.InvariantCulture, "pstk_evt_{0}_{1}_{2}", eventType, reference, status ?? "unknown")
                : ComputePayloadHash(rawPayload);

        var safeMeta = JsonSerializer.Serialize(new
        {
            transfer_code = transferCode,
            reference = reference,
            status = status,
            event_type = eventType,
            account_number = accountNumber
        });

        return new ParsedWebhookPayload(
            ProviderEventId: providerEventId,
            EventType: eventType,
            Reference: reference,
            ProviderReference: transferCode ?? reference,
            AccountNumber: accountNumber,
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
        string? AccountNumber,
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

    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Successfully credited inbound deposit of {Amount} {Currency} to wallet {WalletId}")]
    private static partial void LogVirtualAccountDepositSuccess(ILogger logger, decimal amount, string currency, Guid walletId);

    [LoggerMessage(EventId = 11, Level = LogLevel.Error, Message = "Failed to credit virtual account deposit {EventId}")]
    private static partial void LogVirtualAccountDepositException(ILogger logger, string eventId, Exception exception);

    [LoggerMessage(EventId = 12, Level = LogLevel.Information, Message = "Successfully credited card funding {Ref} of {Amount} {Currency} to wallet {WalletId}")]
    private static partial void LogCardFundingSuccess(ILogger logger, string @ref, decimal amount, string currency, Guid walletId);

    [LoggerMessage(EventId = 13, Level = LogLevel.Error, Message = "Failed to apply card funding credit for {Ref}")]
    private static partial void LogCardFundingException(ILogger logger, string @ref, Exception exception);
}
