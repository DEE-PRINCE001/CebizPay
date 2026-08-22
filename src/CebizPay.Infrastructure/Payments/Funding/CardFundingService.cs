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
/// Service implementation for initializing and reconciling card funding payments.
/// </summary>
public sealed partial class CardFundingService : ICardFundingService
{
    private readonly IEnumerable<ICardPaymentProvider> _providers;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILedgerPostingService _ledgerPosting;
    private readonly IOutboxService _outbox;
    private readonly ILogger<CardFundingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardFundingService"/> class.
    /// </summary>
    public CardFundingService(
        IEnumerable<ICardPaymentProvider> providers,
        ApplicationDbContext dbContext,
        ILedgerPostingService ledgerPosting,
        IOutboxService outbox,
        ILogger<CardFundingService> logger)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
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
        PaymentProvider provider,
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

        var providerRef = $"CBZCD-{Guid.NewGuid():N}";
        var fundingTx = FundingTransaction.Create(
            walletId: walletId,
            virtualAccountId: null,
            provider: provider,
            providerTransactionReference: providerRef,
            fundingChannel: FundingChannel.Card,
            amount: amount,
            currency: currency);

        var providerAdapter = GetProvider(provider);
        var request = new CardPaymentInitializationRequest(
            Amount: amount,
            Currency: currency,
            Email: email,
            Reference: providerRef,
            CallbackUrl: callbackUrl,
            CustomerName: name);

        var result = await providerAdapter.InitializeCardPaymentAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.AuthorizationUrl))
        {
            var providerName = provider.ToString();
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
            afterJson: $"{{\"provider\":\"{provider}\",\"reference\":\"{providerRef}\",\"amount\":{amount}}}",
            organizationId: orgId);
        _dbContext.AuditLogs.Add(audit);

        _outbox.Write(new CardFundingInitiatedDomainEvent(
            FundingTransactionId: fundingTx.Id,
            WalletId: walletId,
            Amount: amount,
            Currency: currency,
            Provider: provider,
            ProviderTransactionReference: providerRef,
            OccurredOnUtc: DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var currStr = currency.ToString();
        LogCardFundingInitSuccess(_logger, providerRef, amount, currStr);

        return new CardFundingInitializationResponse(
            FundingTransactionId: fundingTx.Id,
            Reference: providerRef,
            AuthorizationUrl: result.AuthorizationUrl,
            Provider: provider.ToString());
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
            await using var dbTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var (txn, funding) = await _ledgerPosting.PostInboundFundingCreditCoreAsync(
                    walletId: fundingTx.WalletId,
                    virtualAccountId: null,
                    amount: fundingTx.Amount,
                    currency: fundingTx.Currency,
                    provider: fundingTx.Provider,
                    providerTransactionReference: fundingTx.ProviderTransactionReference,
                    channel: FundingChannel.Card,
                    description: $"Card deposit reconciliation {fundingTx.ProviderTransactionReference}",
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                var audit = AuditLog.Create(
                    actorId: "SYSTEM",
                    action: AuditActions.CardFundingCompleted,
                    resourceType: AuditResourceTypes.FundingTransaction,
                    resourceId: fundingTx.Id.ToString(),
                    afterJson: $"{{\"providerReference\":\"{fundingTx.ProviderTransactionReference}\",\"amount\":{fundingTx.Amount}}}");
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
