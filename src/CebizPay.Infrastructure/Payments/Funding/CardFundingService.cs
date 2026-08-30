using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Payments.Events;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Payments.Funding;

/// <summary>
/// Service implementation for initializing, charging tokenized cards, and reconciling card funding payments.
/// Enforces capability-based provider routing (Flutterwave primary, Paystack fallback on technical failure),
/// PlatformFeePolicy evaluation, and central double-entry ledger settlement.
/// </summary>
public sealed partial class CardFundingService : ICardFundingService
{
    private readonly IEnumerable<ICardPaymentProvider> _providers;
    private readonly IPaymentRoutingService _routingService;
    private readonly IPlatformFeePolicyService _feePolicyService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILedgerPostingService _ledgerPosting;
    private readonly IOutboxService _outbox;
    private readonly ILogger<CardFundingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardFundingService"/> class.
    /// </summary>
    public CardFundingService(
        IEnumerable<ICardPaymentProvider> providers,
        IPaymentRoutingService routingService,
        IPlatformFeePolicyService feePolicyService,
        ApplicationDbContext dbContext,
        ILedgerPostingService ledgerPosting,
        IOutboxService outbox,
        ILogger<CardFundingService> logger)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _routingService = routingService ?? throw new ArgumentNullException(nameof(routingService));
        _feePolicyService = feePolicyService ?? throw new ArgumentNullException(nameof(feePolicyService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _ledgerPosting = ledgerPosting ?? throw new ArgumentNullException(nameof(ledgerPosting));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private ICardPaymentProvider GetProvider(PaymentProvider provider)
    {
        var match = _providers.FirstOrDefault(p => p.Provider == provider);
        if (match == null)
        {
            throw new InvalidOperationException($"No card payment provider registered for '{provider}'.");
        }
        return match;
    }

    /// <inheritdoc/>
    public async Task<CardFundingInitializationResponse> InitializeCardFundingAsync(
        Guid walletId,
        decimal amount,
        Currency currency,
        PaymentProvider? provider,
        string callbackUrl,
        CancellationToken cancellationToken = default)
    {
        if (walletId == Guid.Empty)
            throw new ArgumentException("WalletId is required.", nameof(walletId));
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));
        if (string.IsNullOrWhiteSpace(callbackUrl))
            throw new ArgumentException("CallbackUrl is required.", nameof(callbackUrl));

        currency.EnsureTransactionalV1();

        var wallet = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.Id == walletId, cancellationToken)
            .ConfigureAwait(false);

        if (wallet == null)
            throw new InvalidOperationException($"Wallet '{walletId}' not found.");
        if (wallet.Status != WalletStatus.Active)
            throw new InvalidOperationException($"Wallet '{walletId}' is not active.");
        if (wallet.Currency != currency)
            throw new InvalidOperationException($"Wallet currency '{wallet.Currency}' does not match requested currency '{currency}'.");

        // Determine owner email/name for receipt
        string email = "customer@cebizpay.internal";
        string? name = null;
        string actorId = "SYSTEM";
        Guid? orgId = null;

        if (!string.IsNullOrWhiteSpace(wallet.IndividualId))
        {
            actorId = wallet.IndividualId;
            var profile = await _dbContext.IndividualProfiles
                .FirstOrDefaultAsync(p => p.UserId == wallet.IndividualId, cancellationToken)
                .ConfigureAwait(false);
            if (profile != null)
            {
                email = $"{wallet.IndividualId}@cebizpay.internal";
                name = $"{profile.FirstName} {profile.LastName}".Trim();
            }
        }
        else if (wallet.OrganizationId.HasValue)
        {
            actorId = wallet.OrganizationId.Value.ToString();
            orgId = wallet.OrganizationId.Value;
            var org = await _dbContext.Organizations
                .FirstOrDefaultAsync(o => o.Id == wallet.OrganizationId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (org != null)
            {
                email = org.Email;
                name = org.CompanyName;
            }
        }

        // Calculate platform fee breakdown for card funding
        var feePolicy = await _feePolicyService.GetActivePolicyAsync(FeeOperationType.CardFunding, cancellationToken).ConfigureAwait(false);
        decimal feeAmount = 0m;
        decimal netCreditedAmount = amount;
        Guid? feePolicyId = null;
        int? feePolicyVersion = null;
        FeeBearer? feeBearer = FeeBearer.CustomerPays;

        if (feePolicy != null)
        {
            var breakdown = feePolicy.CalculateBreakdown(amount, currency);
            feeAmount = breakdown.Fee;
            netCreditedAmount = breakdown.NetBeneficiaryCredit;
            feePolicyId = feePolicy.Id;
            feePolicyVersion = feePolicy.Version;
            feeBearer = feePolicy.FeeBearer;
        }

        // Resolve primary provider via capability routing if not explicitly specified
        var selectedProvider = provider ?? _routingService.ResolvePrimaryProvider(PaymentCapability.CardFunding);
        var providerRef = $"CBZCD-{Guid.NewGuid():N}";

        var fundingTx = FundingTransaction.Create(
            walletId: walletId,
            virtualAccountId: null,
            provider: selectedProvider,
            providerTransactionReference: providerRef,
            fundingChannel: FundingChannel.Card,
            amount: amount,
            currency: currency);

        var providerAdapter = GetProvider(selectedProvider);
        var request = new CardPaymentInitializationRequest(
            Amount: amount,
            Currency: currency,
            Email: email,
            Reference: providerRef,
            CallbackUrl: callbackUrl,
            CustomerName: name);

        CardPaymentInitializationResult result;
        try
        {
            result = await providerAdapter.InitializeCardPaymentAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogCardFundingInitFailed(_logger, selectedProvider.ToString(), ex.Message);
            result = CardPaymentInitializationResult.Failure(ex.Message);
        }

        // Fallback routing if primary returned technical failure
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.AuthorizationUrl))
        {
            var fallback = _routingService.GetNextFallbackProvider(PaymentCapability.CardFunding, selectedProvider);
            if (fallback.HasValue && fallback.Value != selectedProvider)
            {
                var fallbackAdapter = GetProvider(fallback.Value);
                selectedProvider = fallback.Value;
                fundingTx = FundingTransaction.Create(
                    walletId: walletId,
                    virtualAccountId: null,
                    provider: selectedProvider,
                    providerTransactionReference: providerRef,
                    fundingChannel: FundingChannel.Card,
                    amount: amount,
                    currency: currency);

                result = await fallbackAdapter.InitializeCardPaymentAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }

        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.AuthorizationUrl))
        {
            var providerName = selectedProvider.ToString();
            var errorMsg = result.ErrorMessage ?? "Unknown error";
            LogCardFundingInitFailed(_logger, providerName, errorMsg);
            throw new InvalidOperationException($"Card funding initialization failed: {result.ErrorMessage}");
        }

        _dbContext.FundingTransactions.Add(fundingTx);

        var audit = AuditLog.Create(
            actorId: actorId,
            action: AuditActions.CardFundingInitiated,
            resourceType: AuditResourceTypes.FundingTransaction,
            resourceId: fundingTx.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new
            {
                Provider = selectedProvider.ToString(),
                Reference = providerRef,
                Amount = amount,
                FeeAmount = feeAmount,
                NetCreditedAmount = netCreditedAmount
            }),
            organizationId: orgId);
        _dbContext.AuditLogs.Add(audit);

        _outbox.Write(new CardFundingInitiatedDomainEvent(
            FundingTransactionId: fundingTx.Id,
            WalletId: walletId,
            Amount: amount,
            Currency: currency,
            Provider: selectedProvider,
            ProviderTransactionReference: providerRef,
            OccurredOnUtc: DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var currStr = currency.ToString();
        LogCardFundingInitSuccess(_logger, providerRef, amount, currStr);

        return new CardFundingInitializationResponse(
            FundingTransactionId: fundingTx.Id,
            Reference: providerRef,
            AuthorizationUrl: result.AuthorizationUrl,
            Provider: selectedProvider.ToString());
    }

    /// <inheritdoc/>
    public async Task<ChargeSavedCardResponseDto> ChargeSavedCardAsync(
        Guid savedCardId,
        decimal amount,
        Currency currency,
        string idempotencyKey,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (savedCardId == Guid.Empty)
            throw new ArgumentException("SavedCardId is required.", nameof(savedCardId));
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(actorUserId))
            throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));

        currency.EnsureTransactionalV1();

        var savedCard = await _dbContext.SavedCards
            .FirstOrDefaultAsync(c => c.Id == savedCardId && c.UserId == actorUserId.Trim(), cancellationToken)
            .ConfigureAwait(false);

        if (savedCard == null)
            throw new InvalidOperationException($"SavedCard '{savedCardId}' not found for user.");

        if (savedCard.Status != SavedCardStatus.Active)
            throw new InvalidOperationException($"Saved card is {savedCard.Status} and cannot be charged.");

        var wallet = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.Id == savedCard.WalletId, cancellationToken)
            .ConfigureAwait(false);

        if (wallet == null)
            throw new InvalidOperationException($"Wallet '{savedCard.WalletId}' not found.");
        if (wallet.Status != WalletStatus.Active)
            throw new InvalidOperationException($"Wallet '{savedCard.WalletId}' is not active.");
        if (wallet.Currency != currency)
            throw new InvalidOperationException($"Wallet currency '{wallet.Currency}' does not match requested charge currency '{currency}'.");

        // Calculate fee breakdown
        var feePolicy = await _feePolicyService.GetActivePolicyAsync(FeeOperationType.CardFunding, cancellationToken).ConfigureAwait(false);
        decimal feeAmount = 0m;
        decimal netCreditedAmount = amount;
        Guid? feePolicyId = null;
        int? feePolicyVersion = null;
        FeeBearer? feeBearer = FeeBearer.CustomerPays;

        if (feePolicy != null)
        {
            var breakdown = feePolicy.CalculateBreakdown(amount, currency);
            feeAmount = breakdown.Fee;
            netCreditedAmount = breakdown.NetBeneficiaryCredit;
            feePolicyId = feePolicy.Id;
            feePolicyVersion = feePolicy.Version;
            feeBearer = feePolicy.FeeBearer;
        }

        var providerRef = $"CBZCD-SAVED-{Guid.NewGuid():N}";
        var fundingTx = FundingTransaction.Create(
            walletId: wallet.Id,
            virtualAccountId: null,
            provider: savedCard.Provider,
            providerTransactionReference: providerRef,
            fundingChannel: FundingChannel.Card,
            amount: amount,
            currency: currency);

        _dbContext.FundingTransactions.Add(fundingTx);

        var auditInitiated = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.CardFundingInitiated,
            resourceType: AuditResourceTypes.FundingTransaction,
            resourceId: fundingTx.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new
            {
                Provider = savedCard.Provider.ToString(),
                SavedCardId = savedCard.Id,
                Reference = providerRef,
                Amount = amount
            }));
        _dbContext.AuditLogs.Add(auditInitiated);

        _outbox.Write(new CardFundingInitiatedDomainEvent(
            FundingTransactionId: fundingTx.Id,
            WalletId: wallet.Id,
            Amount: amount,
            Currency: currency,
            Provider: savedCard.Provider,
            ProviderTransactionReference: providerRef,
            OccurredOnUtc: DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Execute charge on primary provider
        var providerAdapter = GetProvider(savedCard.Provider);
        var email = $"{actorUserId}@cebizpay.internal";
        var chargeRequest = new CardSavedChargeRequest(
            ProviderToken: savedCard.ProviderToken,
            Amount: amount,
            Currency: currency,
            Email: email,
            Reference: providerRef,
            CustomerName: savedCard.CardHolderName);

        var chargeResult = await providerAdapter.ChargeSavedCardAsync(chargeRequest, cancellationToken).ConfigureAwait(false);

        if (chargeResult.Status == PaymentProviderResultStatus.Success)
        {
            // Post central double-entry ledger credit
            var (txn, completedFunding) = await _ledgerPosting.PostCardFundingCreditCoreAsync(
                walletId: wallet.Id,
                grossAmount: amount,
                feeAmount: feeAmount,
                netCreditedAmount: netCreditedAmount,
                providerFeeAmount: 0m,
                currency: currency,
                provider: savedCard.Provider,
                providerTransactionReference: providerRef,
                providerEventReference: null,
                feePolicyId: feePolicyId,
                feePolicyVersion: feePolicyVersion,
                feeBearer: feeBearer,
                description: $"Saved card charge via {savedCard.Provider} ({savedCard.Last4})",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var auditCompleted = AuditLog.Create(
                actorId: actorUserId,
                action: AuditActions.CardFundingCompleted,
                resourceType: AuditResourceTypes.FundingTransaction,
                resourceId: completedFunding.Id.ToString(),
                afterJson: JsonSerializer.Serialize(new
                {
                    completedFunding.Id,
                    LedgerTransactionId = txn.Id,
                    GrossAmount = amount,
                    FeeAmount = feeAmount,
                    NetCreditedAmount = netCreditedAmount
                }));
            _dbContext.AuditLogs.Add(auditCompleted);

            _outbox.Write(new CardFundingCompletedDomainEvent(
                FundingTransactionId: completedFunding.Id,
                WalletId: wallet.Id,
                LedgerTransactionId: txn.Id,
                Amount: amount,
                Currency: currency,
                Provider: savedCard.Provider,
                ProviderTransactionReference: providerRef,
                OccurredOnUtc: DateTime.UtcNow));

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new ChargeSavedCardResponseDto(
                FundingTransactionId: completedFunding.Id,
                Reference: providerRef,
                Status: "Completed",
                GrossAmount: amount,
                FeeAmount: feeAmount,
                NetCreditedAmount: netCreditedAmount,
                Currency: currency.ToString(),
                Provider: savedCard.Provider.ToString());
        }

        if (chargeResult.Status == PaymentProviderResultStatus.BusinessFailure)
        {
            var reason = chargeResult.FailureReason ?? "Card charge failed";
            fundingTx.MarkFailed(reason);

            // Invalidate token if provider reported token failure
            if (reason.Contains("invalid", StringComparison.OrdinalIgnoreCase) || reason.Contains("expired", StringComparison.OrdinalIgnoreCase))
            {
                savedCard.MarkInvalid();
            }

            var auditFailed = AuditLog.Create(
                actorId: actorUserId,
                action: AuditActions.CardFundingFailed,
                resourceType: AuditResourceTypes.FundingTransaction,
                resourceId: fundingTx.Id.ToString(),
                afterJson: JsonSerializer.Serialize(new { fundingTx.Id, Reason = reason }));
            _dbContext.AuditLogs.Add(auditFailed);

            _outbox.Write(new CardFundingFailedDomainEvent(
                FundingTransactionId: fundingTx.Id,
                WalletId: wallet.Id,
                Amount: amount,
                Currency: currency,
                Provider: savedCard.Provider,
                ProviderTransactionReference: providerRef,
                Reason: reason,
                OccurredOnUtc: DateTime.UtcNow));

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new ChargeSavedCardResponseDto(
                FundingTransactionId: fundingTx.Id,
                Reference: providerRef,
                Status: "Failed",
                GrossAmount: amount,
                FeeAmount: feeAmount,
                NetCreditedAmount: 0m,
                Currency: currency.ToString(),
                Provider: savedCard.Provider.ToString());
        }

        // Unknown / Ambiguous status - MUST NOT trigger fallback charge; mark Unknown and reconcile
        var unknownReason = chargeResult.FailureReason ?? "Transaction outcome unknown, pending reconciliation";
        fundingTx.MarkUnknown(unknownReason);

        var auditUnknown = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.CardFundingInitiated,
            resourceType: AuditResourceTypes.FundingTransaction,
            resourceId: fundingTx.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new { fundingTx.Id, Status = "Unknown", Reason = unknownReason }));
        _dbContext.AuditLogs.Add(auditUnknown);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ChargeSavedCardResponseDto(
            FundingTransactionId: fundingTx.Id,
            Reference: providerRef,
            Status: "Unknown",
            GrossAmount: amount,
            FeeAmount: feeAmount,
            NetCreditedAmount: 0m,
            Currency: currency.ToString(),
            Provider: savedCard.Provider.ToString());
    }

    /// <inheritdoc/>
    public async Task<PaymentProviderResult> ReconcileCardFundingAsync(
        Guid fundingTransactionId,
        CancellationToken cancellationToken = default)
    {
        var fundingTx = await _dbContext.FundingTransactions
            .FirstOrDefaultAsync(f => f.Id == fundingTransactionId, cancellationToken)
            .ConfigureAwait(false);

        if (fundingTx == null)
            throw new InvalidOperationException($"FundingTransaction '{fundingTransactionId}' not found.");

        if (fundingTx.Status == FundingTransactionStatus.Completed)
            return PaymentProviderResult.Success(fundingTx.ProviderTransactionReference);

        var providerAdapter = GetProvider(fundingTx.Provider);
        var queryResult = await providerAdapter.GetCardPaymentStatusAsync(fundingTx.ProviderTransactionReference, cancellationToken).ConfigureAwait(false);

        if (queryResult.Status == PaymentProviderResultStatus.Success)
        {
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
                var (txn, funding) = await _ledgerPosting.PostCardFundingCreditCoreAsync(
                    walletId: fundingTx.WalletId,
                    grossAmount: fundingTx.Amount,
                    feeAmount: feeAmount,
                    netCreditedAmount: netCreditedAmount,
                    providerFeeAmount: 0m,
                    currency: fundingTx.Currency,
                    provider: fundingTx.Provider,
                    providerTransactionReference: fundingTx.ProviderTransactionReference,
                    providerEventReference: null,
                    feePolicyId: feePolicyId,
                    feePolicyVersion: feePolicyVersion,
                    feeBearer: feeBearer,
                    description: $"Card deposit reconciliation {fundingTx.ProviderTransactionReference}",
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                var audit = AuditLog.Create(
                    actorId: "SYSTEM",
                    action: AuditActions.CardFundingCompleted,
                    resourceType: AuditResourceTypes.FundingTransaction,
                    resourceId: fundingTx.Id.ToString(),
                    afterJson: JsonSerializer.Serialize(new
                    {
                        ProviderReference = fundingTx.ProviderTransactionReference,
                        GrossAmount = fundingTx.Amount,
                        FeeAmount = feeAmount,
                        NetCreditedAmount = netCreditedAmount
                    }));
                _dbContext.AuditLogs.Add(audit);

                _outbox.Write(new CardFundingCompletedDomainEvent(
                    FundingTransactionId: fundingTx.Id,
                    WalletId: fundingTx.WalletId,
                    LedgerTransactionId: txn.Id,
                    Amount: fundingTx.Amount,
                    Currency: fundingTx.Currency,
                    Provider: fundingTx.Provider,
                    ProviderTransactionReference: fundingTx.ProviderTransactionReference,
                    OccurredOnUtc: DateTime.UtcNow));

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await dbTx.CommitAsync(cancellationToken).ConfigureAwait(false);

                return PaymentProviderResult.Success(fundingTx.ProviderTransactionReference);
            }
            catch (Exception ex)
            {
                await dbTx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                LogCardFundingCreditException(_logger, fundingTransactionId, ex);
                throw;
            }
        }

        if (queryResult.Status == PaymentProviderResultStatus.BusinessFailure)
        {
            var reason = queryResult.FailureReason ?? "Payment failed at gateway";
            fundingTx.MarkFailed(reason);

            _outbox.Write(new CardFundingFailedDomainEvent(
                FundingTransactionId: fundingTx.Id,
                WalletId: fundingTx.WalletId,
                Amount: fundingTx.Amount,
                Currency: fundingTx.Currency,
                Provider: fundingTx.Provider,
                ProviderTransactionReference: fundingTx.ProviderTransactionReference,
                Reason: reason,
                OccurredOnUtc: DateTime.UtcNow));

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return queryResult;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to initialize card funding with {Provider}: {Error}")]
    private static partial void LogCardFundingInitFailed(ILogger logger, string provider, string error);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Card funding initialized: Ref {Reference}, Amount {Amount} {Currency}")]
    private static partial void LogCardFundingInitSuccess(ILogger logger, string reference, decimal amount, string currency);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed to apply reconciled card funding credit for {Id}")]
    private static partial void LogCardFundingCreditException(ILogger logger, Guid id, Exception exception);
}
