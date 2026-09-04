using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Payments.Events;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Payments.Funding;

/// <summary>
/// Infrastructure service managing card payment refunds, provider execution,
/// double-entry ledger reversals, and recovery outstanding states.
/// </summary>
public sealed class CardRefundService : ICardRefundService
{
    private readonly IEnumerable<ICardPaymentProvider> _providers;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILedgerPostingService _ledgerPosting;
    private readonly IOutboxService _outbox;
    private readonly ILogger<CardRefundService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardRefundService"/> class.
    /// </summary>
    public CardRefundService(
        IEnumerable<ICardPaymentProvider> providers,
        ApplicationDbContext dbContext,
        ILedgerPostingService ledgerPosting,
        IOutboxService outbox,
        ILogger<CardRefundService> logger)
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
            throw new InvalidOperationException($"No card payment provider registered for '{provider}'.");
        return match;
    }

    /// <inheritdoc/>
    public async Task<CardRefundResponseDto> RequestCardRefundAsync(
        Guid fundingTransactionId,
        decimal amount,
        string reason,
        string idempotencyKey,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (fundingTransactionId == Guid.Empty)
            throw new ArgumentException("FundingTransactionId is required.", nameof(fundingTransactionId));
        if (amount <= 0)
            throw new ArgumentException("Refund amount must be positive.", nameof(amount));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));

        var cleanKey = idempotencyKey.Trim();

        // Idempotency check
        var existingRefund = await _dbContext.CardRefunds
            .FirstOrDefaultAsync(r => r.IdempotencyKey == cleanKey, cancellationToken)
            .ConfigureAwait(false);

        if (existingRefund != null)
        {
            if (existingRefund.FundingTransactionId != fundingTransactionId || existingRefund.Amount != amount)
            {
                throw new InvalidOperationException($"Idempotency key '{cleanKey}' was already used for a different refund operation.");
            }
            return MapToDto(existingRefund);
        }

        var fundingTx = await _dbContext.FundingTransactions
            .FirstOrDefaultAsync(f => f.Id == fundingTransactionId, cancellationToken)
            .ConfigureAwait(false);

        if (fundingTx == null)
            throw new InvalidOperationException($"FundingTransaction '{fundingTransactionId}' not found.");

        if (!string.IsNullOrWhiteSpace(actorUserId) && actorUserId != "SYSTEM")
        {
            var wallet = await _dbContext.Wallets.AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == fundingTx.WalletId, cancellationToken)
                .ConfigureAwait(false);

            if (wallet == null)
                throw new InvalidOperationException($"Wallet '{fundingTx.WalletId}' not found.");

            bool isAuthorized = false;
            if (wallet.IndividualId == actorUserId)
            {
                isAuthorized = true;
            }
            else if (wallet.OrganizationId.HasValue)
            {
                var membership = await _dbContext.OrganizationMemberships.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.OrganizationId == wallet.OrganizationId.Value && m.UserId == actorUserId && m.Status == Domain.Enums.MembershipStatus.Active, cancellationToken)
                    .ConfigureAwait(false);

                if (membership != null && (membership.Role == Domain.Enums.MembershipRoleType.Owner || membership.Role == Domain.Enums.MembershipRoleType.Admin || membership.HasPermission(Domain.Permissions.Permissions.WalletTransfer)))
                {
                    isAuthorized = true;
                }
            }

            if (!isAuthorized)
            {
                var isAdmin = await _dbContext.AdminProfiles.AsNoTracking()
                    .AnyAsync(a => a.UserId == actorUserId && !a.IsDeleted && a.IsActive, cancellationToken)
                    .ConfigureAwait(false);
                if (isAdmin) isAuthorized = true;
            }

            if (!isAuthorized)
            {
                throw new UnauthorizedAccessException("Caller is not authorized to request a refund for this transaction.");
            }
        }

        if (fundingTx.Status != FundingTransactionStatus.Completed)
            throw new InvalidOperationException($"Cannot refund funding transaction in status '{fundingTx.Status}'. Must be Completed.");

        if (amount > fundingTx.Amount)
            throw new InvalidOperationException($"Refund amount ({amount:F2}) cannot exceed original funding amount ({fundingTx.Amount:F2}).");

        var refundRef = $"CBZRF-{Guid.NewGuid():N}";
        var refund = CardRefund.Create(
            fundingTransactionId: fundingTx.Id,
            walletId: fundingTx.WalletId,
            provider: fundingTx.Provider,
            refundReference: refundRef,
            idempotencyKey: cleanKey,
            amount: amount,
            currency: fundingTx.Currency,
            reason: reason);

        _dbContext.CardRefunds.Add(refund);

        var auditInitiated = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.CardRefundRequested,
            resourceType: AuditResourceTypes.CardRefund,
            resourceId: refund.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new
            {
                refund.Id,
                refund.FundingTransactionId,
                refund.WalletId,
                refund.Amount,
                Currency = refund.Currency.ToString(),
                refund.Reason
            }));
        _dbContext.AuditLogs.Add(auditInitiated);

        _outbox.Write(new CardRefundRequestedDomainEvent(
            RefundId: refund.Id,
            FundingTransactionId: fundingTx.Id,
            WalletId: fundingTx.WalletId,
            Amount: amount,
            Currency: fundingTx.Currency,
            Provider: fundingTx.Provider,
            OccurredOnUtc: DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Execute provider refund outside DB transaction
        var providerAdapter = GetProvider(fundingTx.Provider);
        var providerRefundRequest = new CardRefundRequest(
            ProviderTransactionReference: fundingTx.ProviderTransactionReference,
            Amount: amount,
            Currency: fundingTx.Currency,
            RefundReference: refundRef,
            Reason: reason);

        var providerResult = await providerAdapter.RefundCardPaymentAsync(providerRefundRequest, cancellationToken).ConfigureAwait(false);

        if (providerResult.Succeeded)
        {
            // Reverse on double-entry ledger
            var (txn, settledRefund) = await _ledgerPosting.PostCardRefundReversalCoreAsync(
                refundId: refund.Id,
                fundingTransactionId: fundingTx.Id,
                amount: amount,
                currency: fundingTx.Currency,
                refundReference: refundRef,
                providerRefundReference: providerResult.ProviderRefundReference,
                description: $"Card refund reversal for {fundingTx.ProviderTransactionReference}",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (settledRefund.Status == CardRefundStatus.Succeeded)
            {
                var auditCompleted = AuditLog.Create(
                    actorId: "SYSTEM",
                    action: AuditActions.CardRefundCompleted,
                    resourceType: AuditResourceTypes.CardRefund,
                    resourceId: refund.Id.ToString(),
                    afterJson: JsonSerializer.Serialize(new
                    {
                        refund.Id,
                        LedgerTransactionId = txn.Id,
                        refund.Amount,
                        ProviderRefundReference = providerResult.ProviderRefundReference
                    }));
                _dbContext.AuditLogs.Add(auditCompleted);

                _outbox.Write(new CardRefundCompletedDomainEvent(
                    RefundId: refund.Id,
                    FundingTransactionId: fundingTx.Id,
                    WalletId: fundingTx.WalletId,
                    Amount: amount,
                    Currency: fundingTx.Currency,
                    Provider: fundingTx.Provider,
                    ProviderRefundReference: providerResult.ProviderRefundReference,
                    OccurredOnUtc: DateTime.UtcNow));

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            return MapToDto(settledRefund);
        }
        else
        {
            var failureReason = providerResult.ErrorMessage ?? "Provider refund failed";
            refund.MarkFailed(failureReason);

            var auditFailed = AuditLog.Create(
                actorId: "SYSTEM",
                action: AuditActions.CardRefundFailed,
                resourceType: AuditResourceTypes.CardRefund,
                resourceId: refund.Id.ToString(),
                afterJson: JsonSerializer.Serialize(new { refund.Id, Reason = failureReason }));
            _dbContext.AuditLogs.Add(auditFailed);

            _outbox.Write(new CardRefundFailedDomainEvent(
                RefundId: refund.Id,
                FundingTransactionId: fundingTx.Id,
                WalletId: fundingTx.WalletId,
                Amount: amount,
                Currency: fundingTx.Currency,
                Provider: fundingTx.Provider,
                Reason: failureReason,
                OccurredOnUtc: DateTime.UtcNow));

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return MapToDto(refund);
        }
    }

    /// <inheritdoc/>
    public async Task<CardRefundResponseDto?> GetRefundByIdAsync(
        Guid refundId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (refundId == Guid.Empty)
            throw new ArgumentException("RefundId is required.", nameof(refundId));

        var refund = await _dbContext.CardRefunds
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == refundId, cancellationToken)
            .ConfigureAwait(false);

        if (refund == null) return null;

        if (!string.IsNullOrWhiteSpace(actorUserId) && actorUserId != "SYSTEM")
        {
            var wallet = await _dbContext.Wallets.AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == refund.WalletId, cancellationToken)
                .ConfigureAwait(false);

            bool isAuthorized = false;
            if (wallet != null && wallet.IndividualId == actorUserId)
            {
                isAuthorized = true;
            }
            else if (wallet != null && wallet.OrganizationId.HasValue)
            {
                var membership = await _dbContext.OrganizationMemberships.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.OrganizationId == wallet.OrganizationId.Value && m.UserId == actorUserId && m.Status == Domain.Enums.MembershipStatus.Active, cancellationToken)
                    .ConfigureAwait(false);
                if (membership != null) isAuthorized = true;
            }

            if (!isAuthorized)
            {
                var isAdmin = await _dbContext.AdminProfiles.AsNoTracking()
                    .AnyAsync(a => a.UserId == actorUserId && !a.IsDeleted && a.IsActive, cancellationToken)
                    .ConfigureAwait(false);
                if (isAdmin) isAuthorized = true;
            }

            if (!isAuthorized)
            {
                throw new UnauthorizedAccessException("You do not have permission to view this card refund.");
            }
        }

        return MapToDto(refund);
    }

    /// <inheritdoc/>
    public async Task<CardRefundResponseDto> ReconcileRefundAsync(
        Guid refundId,
        CancellationToken cancellationToken = default)
    {
        var refund = await _dbContext.CardRefunds
            .FirstOrDefaultAsync(r => r.Id == refundId, cancellationToken)
            .ConfigureAwait(false);

        if (refund == null)
            throw new InvalidOperationException($"CardRefund '{refundId}' not found.");

        if (refund.Status == CardRefundStatus.Succeeded)
            return MapToDto(refund);

        if (refund.Status == CardRefundStatus.RecoveryOutstanding)
        {
            // Attempt to reverse again if wallet balance is now sufficient
            var (txn, settledRefund) = await _ledgerPosting.PostCardRefundReversalCoreAsync(
                refundId: refund.Id,
                fundingTransactionId: refund.FundingTransactionId,
                amount: refund.Amount,
                currency: refund.Currency,
                refundReference: refund.RefundReference,
                providerRefundReference: refund.ProviderRefundReference,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return MapToDto(settledRefund);
        }

        return MapToDto(refund);
    }

    private static CardRefundResponseDto MapToDto(CardRefund refund) =>
        new(
            Id: refund.Id,
            FundingTransactionId: refund.FundingTransactionId,
            WalletId: refund.WalletId,
            Provider: refund.Provider.ToString(),
            RefundReference: refund.RefundReference,
            ProviderRefundReference: refund.ProviderRefundReference,
            Amount: refund.Amount,
            Currency: refund.Currency.ToString(),
            Status: refund.Status.ToString(),
            Reason: refund.Reason,
            LedgerTransactionId: refund.LedgerTransactionId,
            FailureReason: refund.FailureReason,
            CreatedAtUtc: refund.CreatedAtUtc,
            CompletedAtUtc: refund.CompletedAtUtc);
}
