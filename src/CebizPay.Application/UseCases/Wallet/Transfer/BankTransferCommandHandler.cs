using System.Text.Json;
using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Finance.Events;
using MediatR;

namespace CebizPay.Application.UseCases.Wallet.Transfer;

/// <summary>
/// Handles <see cref="BankTransferCommand"/>.
/// Implements Option A Immediate Ledger Debit + PENDING lifecycle financial model.
/// Funds and fees are immediately debited from sender's wallet to the Platform Clearing account.
/// The operation is transactional, idempotent, audit-logged, and outbox-evented.
/// </summary>
public sealed class BankTransferCommandHandler : IRequestHandler<BankTransferCommand, BankTransferResponseDto>
{
    private const string OperationName = "BankTransfer";

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITransactionPinService _pinService;
    private readonly IBankTransferFeePolicyService _feePolicyService;
    private readonly IPlatformFeePolicyService? _platformFeePolicyService;
    private readonly ILedgerPostingService _ledgerService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IOutboxService _outboxService;
    private readonly IBankAccountResolver _accountResolver;
    private readonly IBankTransferExecutor? _transferExecutor;

    /// <summary>
    /// Initializes a new instance of <see cref="BankTransferCommandHandler"/>.
    /// </summary>
    public BankTransferCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ITransactionPinService pinService,
        IBankTransferFeePolicyService feePolicyService,
        IPlatformFeePolicyService? platformFeePolicyService,
        ILedgerPostingService ledgerService,
        IIdempotencyService idempotencyService,
        IOutboxService outboxService,
        IBankAccountResolver accountResolver,
        IBankTransferExecutor? transferExecutor = null)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _pinService = pinService;
        _feePolicyService = feePolicyService;
        _platformFeePolicyService = platformFeePolicyService;
        _ledgerService = ledgerService;
        _idempotencyService = idempotencyService;
        _outboxService = outboxService;
        _accountResolver = accountResolver;
        _transferExecutor = transferExecutor;
    }

    /// <summary>
    /// Backward-compatible constructor for testing.
    /// </summary>
    public BankTransferCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ITransactionPinService pinService,
        IBankTransferFeePolicyService feePolicyService,
        ILedgerPostingService ledgerService,
        IIdempotencyService idempotencyService,
        IOutboxService outboxService,
        IBankAccountResolver accountResolver)
        : this(dbContext, currentUserService, pinService, feePolicyService, null, ledgerService, idempotencyService, outboxService, accountResolver, null)
    {
    }

    /// <inheritdoc/>
    public async Task<BankTransferResponseDto> Handle(BankTransferCommand request, CancellationToken cancellationToken)
    {
        // ─── 1. Authenticate user ─────────────────────────────────────────────
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required to perform a bank transfer.");

        // ─── 2. Parse & Validate Currency ─────────────────────────────────────
        if (!Enum.TryParse<Currency>(request.Currency, ignoreCase: true, out var currency) || !currency.IsTransactionalV1())
        {
            throw new ArgumentException(
                $"Currency '{request.Currency}' is not supported for bank transfers. Only V1 transactional currencies (NGN, INTERNATIONAL_NGN, USDT) are allowed.");
        }

        // ─── 3. Compliance & Source Wallet Resolution ─────────────────────────
        CebizPay.Domain.Finance.Entities.Wallet sourceWallet;
        var orgId = request.OrganizationContext;

        if (orgId.HasValue)
        {
            // Corporate Context: Check organization verification status
            var org = await _dbContext.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId.Value && !o.IsDeleted, cancellationToken)
                ?? throw new TransferNotAuthorizedException($"Organization '{orgId.Value}' not found or deleted.");

            if (!org.CanExecuteWalletTransfers())
            {
                throw new ComplianceRestrictedException(
                    $"Organization '{org.CompanyName}' is in '{org.Status}' status and must be Verified before executing bank transfers.");
            }

            // Membership & permission check
            var membership = await _dbContext.OrganizationMemberships
                .FirstOrDefaultAsync(m => m.OrganizationId == orgId.Value && m.UserId == userId && m.Status == MembershipStatus.Active, cancellationToken)
                ?? throw new TransferNotAuthorizedException(
                    "You are not an active member of the specified organization.");

            if (!membership.HasPermission(Domain.Permissions.Permissions.WalletTransferBank) &&
                !membership.HasPermission(Domain.Permissions.Permissions.WalletTransfer))
            {
                throw new TransferNotAuthorizedException(
                    "Organization membership does not have permission to execute bank transfers.");
            }

            sourceWallet = await _dbContext.Wallets
                .FirstOrDefaultAsync(w => w.OrganizationId == orgId.Value && w.Currency == currency, cancellationToken)
                ?? throw new TransferNotAuthorizedException(
                    $"No {currency} wallet found for organization '{orgId.Value}'.");
        }
        else
        {
            // Individual Context: Check profile & Tier limit cap
            var profile = await _dbContext.IndividualProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
                ?? throw new TransferNotAuthorizedException("Individual profile not found for authenticated user.");

            // Unverified individual accounts are capped at < ₦50,000
            if (profile.KycStatus != KycStatus.Verified && request.Amount >= 50000m)
            {
                throw new ComplianceRestrictedException(
                    "Unverified individual accounts cannot transfer ₦50,000 or more via bank transfer. Please complete KYC verification.");
            }

            sourceWallet = await _dbContext.Wallets
                .FirstOrDefaultAsync(w => w.IndividualId == userId && w.Currency == currency, cancellationToken)
                ?? throw new TransferNotAuthorizedException(
                    $"No {currency} wallet found for the authenticated user.");
        }

        // ─── 4. Validate source wallet status ────────────────────────────────
        if (sourceWallet.Status != Domain.Finance.Enums.WalletStatus.Active)
            throw new WalletNotActiveException("Source", sourceWallet.Status.ToString());

        // ─── 5. Resolve & validate destination bank account format ────────────
        var destinationRes = await _accountResolver.ResolveAsync(
            request.DestinationBankCode,
            request.DestinationAccountNumber,
            cancellationToken);

        if (!destinationRes.Succeeded)
        {
            throw new ArgumentException(
                destinationRes.ErrorMessage ?? "Destination bank account details could not be validated.");
        }

        // ─── 6. Verify Transaction PIN ────────────────────────────────────────
        var pinResult = await _pinService.VerifyPinAsync(userId, request.TransactionPin, cancellationToken);
        if (!pinResult.Succeeded)
        {
            if (pinResult.IsLocked)
                throw new PinLockedException(pinResult.Error ?? "Transaction PIN debit lock is active.");

            if (string.IsNullOrEmpty(pinResult.Error) ||
                pinResult.Error.Contains("has not been set", StringComparison.OrdinalIgnoreCase))
                throw new PinRequiredException();

            throw new InvalidPinException(pinResult.Error ?? "Invalid transaction PIN.");
        }

        // ─── 7. Calculate Fee ─────────────────────────────────────────────────
        decimal feeAmount = 0m;
        int? feePolicyVersion = null;
        Guid? feePolicyId = null;

        if (_platformFeePolicyService != null)
        {
            var policy = await _platformFeePolicyService.GetActivePolicyAsync(FeeOperationType.BankTransfer, cancellationToken);
            if (policy != null)
            {
                feeAmount = policy.CalculateFee(request.Amount, currency);
                feePolicyVersion = policy.Version;
                feePolicyId = policy.Id;
            }
        }
        else if (_feePolicyService != null)
        {
            var feePolicy = await _feePolicyService.GetActivePolicyAsync(cancellationToken);
            feeAmount = feePolicy?.CalculateFee(request.Amount, currency) ?? 0m;
            feePolicyVersion = feePolicy?.Version;
            feePolicyId = feePolicy?.Id;
        }

        var totalDebit = request.Amount + feeAmount;

        // ─── 8. Optimistic pre-check on balance ──────────────────────────────
        if (sourceWallet.AvailableBalance < totalDebit)
            throw new InsufficientFundsException(sourceWallet.AvailableBalance, totalDebit);

        // ─── 9. Begin Database Transaction ────────────────────────────────────
        await using var dbTx = await _dbContext.BeginTransactionAsync(cancellationToken);

        try
        {
            // ─── 10. Idempotency Check & Insert ───────────────────────────────
            var requestPayload = JsonSerializer.Serialize(new
            {
                request.DestinationBankCode,
                DestinationAccountNumber = request.DestinationAccountNumber.Trim(),
                request.Amount,
                Currency = currency.ToString(),
                SourceWalletId = sourceWallet.Id,
                FeePolicyVersion = feePolicyVersion
            });

            var idempotencyRecord = await _idempotencyService.CreateRecordAsync(
                request.IdempotencyKey,
                OperationName,
                requestPayload,
                userId,
                orgId,
                autoSave: true,
                cancellationToken: cancellationToken);

            if (idempotencyRecord.Status == IdempotencyStatus.Completed && idempotencyRecord.ResponseJson != null)
            {
                await dbTx.RollbackAsync(cancellationToken);
                return JsonSerializer.Deserialize<BankTransferResponseDto>(idempotencyRecord.ResponseJson)
                    ?? throw new InvalidOperationException("Failed to deserialize cached idempotency response.");
            }

            if (idempotencyRecord.Status == IdempotencyStatus.Processing)
            {
                await dbTx.RollbackAsync(cancellationToken);
                throw new IdempotencyConflictException(
                    request.IdempotencyKey,
                    $"A bank transfer request with idempotency key '{request.IdempotencyKey}' is currently being processed.");
            }

            // ─── 11. Ensure Clearing Account & Fee Account exist ──────────────
            var clearingAccount = await _ledgerService.GetOrCreateBankTransferClearingAccountAsync(currency, cancellationToken);
            var feeAccount = await _ledgerService.GetOrCreatePlatformFeeAccountAsync(currency, cancellationToken);

            // ─── 12. Generate Transfer Reference & Execute Debit Posting ──────
            var reference = GenerateBankTransferReference();

            var (ledgerTxn, bankTransfer) = await _ledgerService.PostBankTransferDebitCoreAsync(
                senderWalletId: sourceWallet.Id,
                clearingAccountId: clearingAccount.Id,
                platformFeeAccountId: feeAccount.Id,
                transferAmount: request.Amount,
                feeAmount: feeAmount,
                currency: currency,
                destinationBankCode: request.DestinationBankCode,
                destinationAccountNumber: request.DestinationAccountNumber,
                destinationAccountName: destinationRes.AccountName,
                feePolicyId: feePolicyId,
                feePolicyVersion: feePolicyVersion,
                reference: reference,
                idempotencyKey: request.IdempotencyKey,
                description: $"Bank transfer: {request.Amount} {currency} to bank {request.DestinationBankCode}",
                cancellationToken: cancellationToken);

            // ─── 13. Audit Log (Masked Account Number) ────────────────────────
            var maskedAccount = bankTransfer.GetMaskedAccountNumber();

            var auditLog = Domain.Entities.AuditLog.Create(
                actorId: userId,
                action: Domain.Auditing.AuditActions.BankTransferCreated,
                resourceType: Domain.Auditing.AuditResourceTypes.BankTransfer,
                resourceId: bankTransfer.Id.ToString(),
                organizationId: orgId,
                afterJson: JsonSerializer.Serialize(new
                {
                    Reference = bankTransfer.Reference,
                    Amount = request.Amount,
                    Currency = currency.ToString(),
                    FeeAmount = feeAmount,
                    TotalDebited = totalDebit,
                    SenderWalletId = sourceWallet.Id,
                    DestinationBankCode = request.DestinationBankCode,
                    DestinationAccountNumber = maskedAccount,
                    DestinationAccountName = destinationRes.AccountName,
                    AppliedFeePolicyVersion = feePolicyVersion,
                    Status = BankTransferStatus.Pending.ToString()
                }));

            _dbContext.AuditLogs.Add(auditLog);

            // ─── 14. Publish Outbox Event ─────────────────────────────────────
            var domainEvent = new BankTransferCreatedEvent(
                TransferId: bankTransfer.Id,
                TransactionReference: bankTransfer.Reference,
                SenderWalletId: sourceWallet.Id,
                DestinationBankCode: request.DestinationBankCode,
                MaskedDestinationAccountNumber: maskedAccount,
                Amount: request.Amount,
                Currency: currency.ToString(),
                FeeAmount: feeAmount,
                FeeCurrency: currency.ToString(),
                FeePolicyVersion: feePolicyVersion,
                OccurredOnUtc: DateTime.UtcNow);

            _outboxService.Write(domainEvent);

            // ─── 15. Build Response DTO & Complete Idempotency ────────────────
            var response = new BankTransferResponseDto(
                TransactionReference: bankTransfer.Reference,
                Status: BankTransferStatus.Pending.ToString().ToUpperInvariant(),
                Amount: request.Amount,
                Currency: currency.ToString(),
                FeeAmount: feeAmount,
                TotalDebited: totalDebit,
                DestinationBankCode: request.DestinationBankCode,
                DestinationAccountNumber: maskedAccount,
                DestinationAccountName: destinationRes.AccountName,
                AppliedFeePolicyVersion: feePolicyVersion,
                CreatedAtUtc: bankTransfer.CreatedAtUtc);

            idempotencyRecord.Complete(JsonSerializer.Serialize(response));

            // ─── 16. Save & Commit Transaction ────────────────────────────────
            await _dbContext.SaveChangesAsync(cancellationToken);
            await dbTx.CommitAsync(cancellationToken);

            // ─── 17. Dispatch External Provider Execution (Outside DB Lock Boundary) ─
            if (_transferExecutor != null)
            {
                try
                {
                    await _transferExecutor.ExecuteAsync(bankTransfer, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // PaymentAttempt tracks provider outcome/error; transfer creation commit remains valid
                }
            }

            return response;
        }
        catch (InvalidOperationException ex) when (
            ex.Message.StartsWith("Insufficient funds after lock", StringComparison.OrdinalIgnoreCase))
        {
            await dbTx.RollbackAsync(cancellationToken);
            throw new InsufficientFundsException(sourceWallet.AvailableBalance, totalDebit);
        }
        catch
        {
            await dbTx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string GenerateBankTransferReference()
    {
        var shortId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        return $"CBZBT-{shortId}";
    }
}
