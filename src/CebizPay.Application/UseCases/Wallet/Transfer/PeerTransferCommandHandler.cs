using System.Text.Json;
using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Finance.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Application.UseCases.Wallet.Transfer;

/// <summary>
/// Handles the <see cref="PeerTransferCommand"/>.
///
/// Full transfer flow:
///   1. Resolve authenticated user via ICurrentUserService
///   2. Parse and validate currency
///   3. Determine operation scope (individual vs. organization)
///   4. Resolve and authorize source wallet
///   5. Resolve recipient user via IUserLookupService
///   6. Resolve recipient wallet
///   7. Self-transfer check
///   8. Validate wallet statuses (pre-check)
///   9. Validate currency match
///  10. Verify transaction PIN via ITransactionPinService (3 failures → 15-min debit lock)
///  11. Resolve active fee policy and calculate fee
///  12. Pre-validate balance (optimistic check before locking)
///  13. Idempotency check / create record
///  14. Ensure platform fee account exists
///  15. Begin DB transaction → PostPeerTransferCoreAsync (deterministic locking, re-validation, ledger, balance)
///  16. Create AuditLog (who performed the action)
///  17. Publish PeerTransferCompletedEvent via Outbox
///  18. Complete IdempotencyRecord
///  19. Commit
///  20. Return stable response DTO
/// </summary>
public sealed class PeerTransferCommandHandler : IRequestHandler<PeerTransferCommand, PeerTransferResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserLookupService _userLookup;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ITransactionPinService _pinService;
    private readonly IFeePolicyService _feePolicyService;
    private readonly ILedgerPostingService _ledgerService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IOutboxService _outboxService;

    private const string OperationName = "PeerTransfer";

    /// <summary>
    /// Initializes a new instance of <see cref="PeerTransferCommandHandler"/>.
    /// </summary>
    public PeerTransferCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IUserLookupService userLookup,
        ICurrentOrganizationContext orgContext,
        ITransactionPinService pinService,
        IFeePolicyService feePolicyService,
        ILedgerPostingService ledgerService,
        IIdempotencyService idempotencyService,
        IOutboxService outboxService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _userLookup = userLookup;
        _orgContext = orgContext;
        _pinService = pinService;
        _feePolicyService = feePolicyService;
        _ledgerService = ledgerService;
        _idempotencyService = idempotencyService;
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public async Task<PeerTransferResponseDto> Handle(PeerTransferCommand request, CancellationToken cancellationToken)
    {
        // ─── 1. Resolve authenticated user ─────────────────────────────────────
        var userId = _currentUser.UserId
            ?? throw new TransferNotAuthorizedException("Authenticated user context is not available.");

        // ─── 2. Parse and validate currency ──────────────────────────────────
        if (!Enum.TryParse<Currency>(request.Currency, ignoreCase: true, out var currency) || !currency.IsTransactionalV1())
        {
            throw new CurrencyMismatchException(request.Currency, "N/A");
        }

        // ─── 3. Determine operation scope (individual vs. organization) ───────
        var orgId = request.OrganizationContext ?? _orgContext.CurrentOrganizationId;

        // ─── 4. Resolve source wallet ─────────────────────────────────────────
        Domain.Finance.Entities.Wallet sourceWallet;

        if (orgId.HasValue)
        {
            // Organization context: validate membership and organization status
            var org = await _dbContext.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId.Value && !o.IsDeleted, cancellationToken)
                ?? throw new TransferNotAuthorizedException($"Organization '{orgId.Value}' not found or deleted.");

            if (!org.CanExecuteWalletTransfers())
                throw new ComplianceRestrictedException(
                    $"Organization '{org.CompanyName}' is not verified and cannot execute outbound wallet transfers.");

            var membership = await _dbContext.OrganizationMemberships
                .FirstOrDefaultAsync(m =>
                    m.OrganizationId == orgId.Value &&
                    m.UserId == userId &&
                    m.Status == MembershipStatus.Active, cancellationToken)
                ?? throw new TransferNotAuthorizedException(
                    $"User is not an active member of organization '{orgId.Value}'.");

            if (!membership.HasPermission(Domain.Permissions.Permissions.WalletTransfer))
                throw new TransferNotAuthorizedException(
                    "Organization membership does not have Wallet.Transfer permission.");

            sourceWallet = await _dbContext.Wallets
                .FirstOrDefaultAsync(w => w.OrganizationId == orgId.Value && w.Currency == currency, cancellationToken)
                ?? throw new TransferNotAuthorizedException(
                    $"No {currency} wallet found for organization '{orgId.Value}'.");
        }
        else
        {
            // Individual context: resolve from the user's individual wallet
            sourceWallet = await _dbContext.Wallets
                .FirstOrDefaultAsync(w => w.IndividualId == userId && w.Currency == currency, cancellationToken)
                ?? throw new TransferNotAuthorizedException(
                    $"No {currency} wallet found for the authenticated user. Please create a wallet first.");
        }

        // ─── 5. Validate source wallet status ────────────────────────────────
        if (sourceWallet.Status != WalletStatus.Active)
            throw new WalletNotActiveException("Source", sourceWallet.Status.ToString());

        // ─── 6. Resolve recipient user ────────────────────────────────────────
        UserSummary? recipientUser;

        if (request.RecipientIdentifier.Contains('@'))
        {
            recipientUser = await _userLookup.FindByEmailAsync(request.RecipientIdentifier, cancellationToken);
        }
        else
        {
            recipientUser = await _userLookup.FindByPhoneAsync(request.RecipientIdentifier, cancellationToken);
        }

        if (recipientUser == null)
            throw new KeyNotFoundException($"Recipient '{request.RecipientIdentifier}' was not found on CebizPay.");

        // Ensure recipient is a registered individual (not just any user)
        var recipientProfile = await _dbContext.IndividualProfiles
            .FirstOrDefaultAsync(p => p.UserId == recipientUser.UserId, cancellationToken)
            ?? throw new TransferNotAuthorizedException("Recipient is not a registered CebizPay individual user.");

        // ─── 7. Resolve recipient wallet ──────────────────────────────────────
        var recipientWallet = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.IndividualId == recipientUser.UserId && w.Currency == currency, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Recipient does not have a {currency} wallet.");

        // ─── 8. Self-transfer check ───────────────────────────────────────────
        if (sourceWallet.Id == recipientWallet.Id)
            throw new SelfTransferException();

        // ─── 9. Validate recipient wallet status ──────────────────────────────
        if (recipientWallet.Status != WalletStatus.Active)
            throw new WalletNotActiveException("Recipient", recipientWallet.Status.ToString());

        // ─── 10. Currency match ────────────────────────────────────────────────
        if (sourceWallet.Currency != recipientWallet.Currency)
            throw new CurrencyMismatchException(sourceWallet.Currency.ToString(), recipientWallet.Currency.ToString());

        // ─── 11. Verify transaction PIN ────────────────────────────────────────
        // Do NOT log the raw PIN value
        var pinResult = await _pinService.VerifyPinAsync(userId, request.TransactionPin, cancellationToken);

        if (pinResult.IsLocked)
            throw new PinLockedException(pinResult.Error ?? "Transaction PIN debit lock is active.");

        if (!pinResult.Succeeded)
        {
            if (string.IsNullOrEmpty(pinResult.Error)
                || pinResult.Error.Contains("has not been set", StringComparison.OrdinalIgnoreCase))
                throw new PinRequiredException();

            throw new InvalidOperationException(pinResult.Error ?? "Invalid transaction PIN.");
        }

        // ─── 12. Resolve active fee policy and calculate fee ──────────────────
        var feePolicy = await _feePolicyService.GetActivePolicyAsync(cancellationToken);
        var feeAmount = feePolicy?.CalculateFee(request.Amount, currency) ?? 0m;
        var feePolicyVersion = feePolicy?.Version;
        var totalDebit = request.Amount + feeAmount;

        // ─── 13. Pre-validate balance (optimistic, before acquiring DB lock) ──
        if (sourceWallet.AvailableBalance < totalDebit)
            throw new InsufficientFundsException(sourceWallet.AvailableBalance, totalDebit);

        // ─── 14. Idempotency check ─────────────────────────────────────────────
        // Hash covers the fields that define the financial operation uniquely
        var requestPayload = JsonSerializer.Serialize(new
        {
            RecipientId = recipientUser.UserId,
            request.Amount,
            Currency = currency.ToString(),
            SourceWalletId = sourceWallet.Id,
            FeePolicyVersion = feePolicyVersion
        });

        var existingRecord = await _idempotencyService.GetRecordAsync(
            request.IdempotencyKey, OperationName, userId, orgId, cancellationToken);

        if (existingRecord != null)
        {
            if (existingRecord.Status == IdempotencyStatus.Completed && existingRecord.ResponseJson != null)
            {
                // Idempotent replay: return original result
                return JsonSerializer.Deserialize<PeerTransferResponseDto>(existingRecord.ResponseJson)
                    ?? throw new InvalidOperationException("Failed to deserialize cached idempotency response.");
            }

            // Same key, different payload → conflict
            if (existingRecord.RequestHash != ComputeHash(requestPayload))
                throw new IdempotencyConflictException(request.IdempotencyKey,
                    $"Idempotency key '{request.IdempotencyKey}' was previously used with a different request payload.");
        }

        var idempotencyRecord = await _idempotencyService.CreateRecordAsync(
            request.IdempotencyKey, OperationName, requestPayload, userId, orgId, cancellationToken);

        // ─── 15. Ensure platform fee ledger account exists (before transaction) ─
        var feeAccount = await _ledgerService.GetOrCreatePlatformFeeAccountAsync(currency, cancellationToken);

        // ─── 16. Generate stable financial reference ───────────────────────────
        var reference = GenerateTransferReference();

        // ─── 17. Begin DB transaction and execute atomic operations ──────────────
        await using var dbTx = await _dbContext.BeginTransactionAsync(cancellationToken);

        try
        {
            // PostPeerTransferCoreAsync: acquires row-level locks on wallets in deterministic order,
            // re-validates balance and wallet status inside the lock (TOCTOU protection),
            // creates 3-entry ledger transaction, and materializes wallet balances.
            var ledgerTxn = await _ledgerService.PostPeerTransferCoreAsync(
                senderWalletId: sourceWallet.Id,
                recipientWalletId: recipientWallet.Id,
                platformFeeAccountId: feeAccount.Id,
                transferAmount: request.Amount,
                feeAmount: feeAmount,
                currency: currency,
                reference: reference,
                idempotencyKey: request.IdempotencyKey,
                description: $"Peer transfer: {request.Amount} {currency}",
                cancellationToken: cancellationToken);

            // ─── 18. Create AuditLog (who performed the action) ───────────────
            var auditLog = new AuditLog(
                actorUserId: userId,
                action: "Wallet.PeerTransfer",
                entityType: "LedgerTransaction",
                entityId: ledgerTxn.Id.ToString(),
                detailsJson: JsonSerializer.Serialize(new
                {
                    Reference = ledgerTxn.Reference,
                    Amount = request.Amount,
                    Currency = currency.ToString(),
                    FeeAmount = feeAmount,
                    TotalDebited = totalDebit,
                    SenderWalletId = sourceWallet.Id,
                    RecipientWalletId = recipientWallet.Id,
                    OrganizationId = orgId
                }));

            _dbContext.AuditLogs.Add(auditLog);

            // ─── 19. Publish PeerTransferCompletedEvent via Outbox ────────────
            // Outbox ensures the event is reliably published after the transaction commits.
            var domainEvent = new PeerTransferCompletedEvent(
                TransactionId: ledgerTxn.Id,
                TransactionReference: ledgerTxn.Reference,
                SenderWalletId: sourceWallet.Id,
                RecipientWalletId: recipientWallet.Id,
                Amount: request.Amount,
                Currency: currency.ToString(),
                FeeAmount: feeAmount,
                FeeCurrency: currency.ToString(),
                FeePolicyVersion: feePolicyVersion,
                OccurredOnUtc: DateTime.UtcNow);

            _outboxService.Write(domainEvent);

            // ─── 20. Build response DTO ────────────────────────────────────────
            var recipientDisplay = !string.IsNullOrWhiteSpace(recipientUser.Email)
                ? recipientUser.Email
                : request.RecipientIdentifier;

            var response = new PeerTransferResponseDto(
                TransactionReference: ledgerTxn.Reference,
                Status: "COMPLETED",
                Amount: request.Amount,
                Currency: currency.ToString(),
                FeeAmount: feeAmount,
                TotalDebited: totalDebit,
                RecipientDisplay: recipientDisplay,
                AppliedFeePolicyVersion: feePolicyVersion,
                CreatedAtUtc: ledgerTxn.CreatedAtUtc);

            // ─── 21. Mark idempotency record complete (within same transaction) ─
            idempotencyRecord.Complete(JsonSerializer.Serialize(response));

            await _dbContext.SaveChangesAsync(cancellationToken);
            await dbTx.CommitAsync(cancellationToken);

            return response;
        }
        catch (InvalidOperationException ex) when (
            ex.Message.StartsWith("Insufficient funds after lock", StringComparison.OrdinalIgnoreCase))
        {
            await dbTx.RollbackAsync(cancellationToken);
            await _idempotencyService.FailRecordAsync(idempotencyRecord.Id, ex.Message, cancellationToken);
            throw new InsufficientFundsException(sourceWallet.AvailableBalance, totalDebit);
        }
        catch
        {
            await dbTx.RollbackAsync(cancellationToken);
            await _idempotencyService.FailRecordAsync(idempotencyRecord.Id, null, cancellationToken);
            throw;
        }
    }

    private static string GenerateTransferReference()
    {
        var shortId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        return $"CBZPT-{shortId}";
    }

    private static string ComputeHash(string payload)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }
}
