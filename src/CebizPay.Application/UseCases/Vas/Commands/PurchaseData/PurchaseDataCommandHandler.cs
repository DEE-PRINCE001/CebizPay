using System.Text.Json;
using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Interfaces.Vas;
using CebizPay.Application.Common.Models.Vas;
using CebizPay.Application.Common.Utils;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Vas.Entities;
using CebizPay.Domain.Vas.Enums;
using CebizPay.Domain.Vas.Events;
using MediatR;

namespace CebizPay.Application.UseCases.Vas.Commands.PurchaseData;

/// <summary>
/// Handles <see cref="PurchaseDataCommand"/>.
/// Implements immediate atomic wallet debit to platform VAS clearing account,
/// enforces 120-second duplicate guard, and dispatches data bundle fulfillment via VTUGATE.
/// </summary>
public sealed class PurchaseDataCommandHandler : IRequestHandler<PurchaseDataCommand, VasPurchaseResponseDto>
{
    private const string OperationName = "VasPurchaseData";

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITransactionPinService _pinService;
    private readonly ILedgerPostingService _ledgerService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IOutboxService _outboxService;
    private readonly IVasDuplicateGuard _duplicateGuard;
    private readonly IVasPurchaseExecutor _purchaseExecutor;

    /// <summary>
    /// Initializes a new instance of <see cref="PurchaseDataCommandHandler"/>.
    /// </summary>
    public PurchaseDataCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ITransactionPinService pinService,
        ILedgerPostingService ledgerService,
        IIdempotencyService idempotencyService,
        IOutboxService outboxService,
        IVasDuplicateGuard duplicateGuard,
        IVasPurchaseExecutor purchaseExecutor)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _pinService = pinService;
        _ledgerService = ledgerService;
        _idempotencyService = idempotencyService;
        _outboxService = outboxService;
        _duplicateGuard = duplicateGuard;
        _purchaseExecutor = purchaseExecutor;
    }

    /// <inheritdoc/>
    public async Task<VasPurchaseResponseDto> Handle(PurchaseDataCommand request, CancellationToken cancellationToken)
    {
        // ─── 1. Authenticate user ─────────────────────────────────────────────
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required to perform a data bundle purchase.");

        // ─── 2. Normalize Phone Number & Parse Network ────────────────────────
        var normalizedPhone = PhoneNormalizer.NormalizeNational(request.PhoneNumber);
        if (string.IsNullOrWhiteSpace(normalizedPhone))
            throw new ArgumentException("Invalid phone number format.", nameof(request));

        if (!Enum.TryParse<VasNetwork>(request.Network, ignoreCase: true, out var network))
        {
            if (request.Network.Equals("9MOBILE", StringComparison.OrdinalIgnoreCase))
                network = VasNetwork.NineMobile;
            else
                throw new ArgumentException($"Unsupported network operator '{request.Network}'.", nameof(request));
        }

        // ─── 3. Compliance & Source Wallet Resolution ─────────────────────────
        Domain.Finance.Entities.Wallet sourceWallet;
        var orgId = request.OrganizationContext;

        if (orgId.HasValue)
        {
            var org = await _dbContext.Organizations
                .FirstOrDefaultAsync(o => o.Id == orgId.Value && !o.IsDeleted, cancellationToken)
                ?? throw new TransferNotAuthorizedException($"Organization '{orgId.Value}' not found or deleted.");

            if (!org.CanExecuteWalletTransfers())
            {
                throw new ComplianceRestrictedException(
                    $"Organization '{org.CompanyName}' is in '{org.Status}' status and must be Verified before executing VAS purchases.");
            }

            var membership = await _dbContext.OrganizationMemberships
                .FirstOrDefaultAsync(m => m.OrganizationId == orgId.Value && m.UserId == userId && m.Status == MembershipStatus.Active, cancellationToken)
                ?? throw new TransferNotAuthorizedException("You are not an active member of the specified organization.");

            if (!membership.HasPermission(Domain.Permissions.Permissions.VasPurchase) &&
                !membership.HasPermission(Domain.Permissions.Permissions.WalletTransfer))
            {
                throw new TransferNotAuthorizedException("Organization membership does not have permission to execute VAS purchases.");
            }

            sourceWallet = await _dbContext.Wallets
                .FirstOrDefaultAsync(w => w.OrganizationId == orgId.Value && w.Currency == Currency.NGN, cancellationToken)
                ?? throw new TransferNotAuthorizedException($"No NGN wallet found for organization '{orgId.Value}'.");
        }
        else
        {
            var profile = await _dbContext.IndividualProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
                ?? throw new TransferNotAuthorizedException("Individual profile not found for authenticated user.");

            sourceWallet = await _dbContext.Wallets
                .FirstOrDefaultAsync(w => w.IndividualId == userId && w.Currency == Currency.NGN, cancellationToken)
                ?? throw new TransferNotAuthorizedException("No NGN wallet found for the authenticated user.");
        }

        if (sourceWallet.Status != Domain.Finance.Enums.WalletStatus.Active)
            throw new WalletNotActiveException("Source", sourceWallet.Status.ToString());

        // ─── 4. Verify Transaction PIN ────────────────────────────────────────
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

        // ─── 5. 120-Second Duplicate Purchase Guard Check ────────────────────
        var acquiredDuplicateLock = await _duplicateGuard.TryAcquireDuplicateLockAsync(
            VasType.Data,
            normalizedPhone,
            request.Amount,
            network,
            request.ProductCode,
            cancellationToken);

        if (!acquiredDuplicateLock)
        {
            throw new VasDuplicatePurchaseException();
        }

        // ─── 6. Balance Pre-Check ─────────────────────────────────────────────
        if (sourceWallet.AvailableBalance < request.Amount)
        {
            await _duplicateGuard.ReleaseDuplicateLockAsync(VasType.Data, normalizedPhone, request.Amount, network, request.ProductCode, cancellationToken);
            throw new InsufficientFundsException(sourceWallet.AvailableBalance, request.Amount);
        }

        // ─── 7. Begin Database Transaction ────────────────────────────────────
        await using var dbTx = await _dbContext.BeginTransactionAsync(cancellationToken);

        Guid vasTransactionId;
        string reference;
        DateTime createdAtUtc;
        var maskedPhone = normalizedPhone.Length >= 7 ? $"{normalizedPhone[..4]}***{normalizedPhone[^4..]}" : normalizedPhone;

        try
        {
            // ─── 8. Idempotency Check & Insert ────────────────────────────────
            var requestPayload = JsonSerializer.Serialize(new
            {
                PhoneNumber = normalizedPhone,
                Network = network.ToString(),
                ProductCode = request.ProductCode.Trim(),
                request.Amount,
                Currency = Currency.NGN.ToString(),
                SourceWalletId = sourceWallet.Id,
                Type = VasType.Data.ToString()
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
                return JsonSerializer.Deserialize<VasPurchaseResponseDto>(idempotencyRecord.ResponseJson)
                    ?? throw new InvalidOperationException("Failed to deserialize cached idempotency response.");
            }

            if (idempotencyRecord.Status == IdempotencyStatus.Processing)
            {
                await dbTx.RollbackAsync(cancellationToken);
                throw new IdempotencyConflictException(
                    request.IdempotencyKey,
                    $"A VAS data purchase request with idempotency key '{request.IdempotencyKey}' is currently being processed.");
            }

            // ─── 9. Ensure VAS Clearing Account Exists ────────────────────────
            var clearingAccount = await _ledgerService.GetOrCreateVasClearingAccountAsync(Currency.NGN, cancellationToken);

            // ─── 10. Generate Reference & Execute Atomic Debit ─────────────────
            reference = GenerateVasReference();
            var productName = $"Data Bundle ({request.ProductCode.Trim()})";

            var (ledgerTxn, vasTxn) = await _ledgerService.PostVasPurchaseDebitCoreAsync(
                customerWalletId: sourceWallet.Id,
                vasClearingAccountId: clearingAccount.Id,
                amount: request.Amount,
                currency: Currency.NGN,
                userId: userId,
                organizationId: orgId,
                phoneNumber: normalizedPhone,
                network: network,
                type: VasType.Data,
                productCode: request.ProductCode.Trim(),
                productName: productName,
                reference: reference,
                idempotencyKey: request.IdempotencyKey,
                description: $"Data purchase: {request.ProductCode} for {maskedPhone} ({network})",
                cancellationToken: cancellationToken);

            vasTransactionId = vasTxn.Id;
            createdAtUtc = vasTxn.CreatedAtUtc;

            // ─── 11. Audit Log ────────────────────────────────────────────────
            var auditLog = AuditLog.Create(
                actorId: userId,
                action: Domain.Auditing.AuditActions.VasPurchaseCreated,
                resourceType: Domain.Auditing.AuditResourceTypes.VasTransaction,
                resourceId: vasTxn.Id.ToString(),
                organizationId: orgId,
                afterJson: JsonSerializer.Serialize(new
                {
                    vasTxn.Reference,
                    vasTxn.Amount,
                    Currency = Currency.NGN.ToString(),
                    Type = VasType.Data.ToString(),
                    Network = network.ToString(),
                    ProductCode = request.ProductCode,
                    PhoneNumber = maskedPhone,
                    Status = VasTransactionStatus.Pending.ToString()
                }));

            _dbContext.AuditLogs.Add(auditLog);

            // ─── 12. Publish Outbox Event ─────────────────────────────────────
            var outboxEvent = new VasPurchaseCreatedEvent(
                VasTransactionId: vasTxn.Id,
                Reference: vasTxn.Reference,
                UserId: userId,
                OrganizationId: orgId,
                WalletId: sourceWallet.Id,
                LedgerTransactionId: ledgerTxn.Id,
                Type: VasType.Data,
                Network: network,
                MaskedPhoneNumber: maskedPhone,
                Amount: request.Amount,
                Currency: Currency.NGN.ToString(),
                ProductCode: request.ProductCode.Trim(),
                OccurredOnUtc: DateTime.UtcNow);

            _outboxService.Write(outboxEvent);

            // ─── 13. Build Initial Response DTO & Complete Idempotency ────────
            var initialResponse = new VasPurchaseResponseDto(
                Reference: vasTxn.Reference,
                Type: VasType.Data.ToString().ToUpperInvariant(),
                Status: VasTransactionStatus.Processing.ToString().ToUpperInvariant(),
                Amount: request.Amount,
                Currency: Currency.NGN.ToString(),
                Network: network.ToString().ToUpperInvariant(),
                MaskedPhoneNumber: maskedPhone,
                ProductCode: request.ProductCode.Trim(),
                ProductName: productName,
                CreatedAtUtc: createdAtUtc);

            idempotencyRecord.Complete(JsonSerializer.Serialize(initialResponse));

            // ─── 14. Commit Financial Database Transaction ────────────────────
            await _dbContext.SaveChangesAsync(cancellationToken);
            await dbTx.CommitAsync(cancellationToken);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.StartsWith("Insufficient funds after lock", StringComparison.OrdinalIgnoreCase))
        {
            await dbTx.RollbackAsync(cancellationToken);
            await _duplicateGuard.ReleaseDuplicateLockAsync(VasType.Data, normalizedPhone, request.Amount, network, request.ProductCode, cancellationToken);
            throw new InsufficientFundsException(sourceWallet.AvailableBalance, request.Amount);
        }
        catch
        {
            await dbTx.RollbackAsync(cancellationToken);
            await _duplicateGuard.ReleaseDuplicateLockAsync(VasType.Data, normalizedPhone, request.Amount, network, request.ProductCode, cancellationToken);
            throw;
        }

        // ─── 15. Dispatch External Fulfillment to VTUGATE outside DB Transaction
        var executionResult = await _purchaseExecutor.ExecutePurchaseAsync(vasTransactionId, cancellationToken);

        var finalStatus = executionResult.Status.ToString().ToUpperInvariant();

        return new VasPurchaseResponseDto(
            Reference: reference,
            Type: VasType.Data.ToString().ToUpperInvariant(),
            Status: finalStatus,
            Amount: request.Amount,
            Currency: Currency.NGN.ToString(),
            Network: network.ToString().ToUpperInvariant(),
            MaskedPhoneNumber: maskedPhone,
            ProductCode: request.ProductCode.Trim(),
            ProductName: $"Data Bundle ({request.ProductCode.Trim()})",
            CreatedAtUtc: createdAtUtc);
    }

    private static string GenerateVasReference()
    {
        var shortId = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        return $"CBZVAS-DAT-{DateTime.UtcNow:yyyyMMddHHmmss}-{shortId}";
    }
}
