using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Card wallet funding initialization, recurring charging, and reconciliation endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/funding/card")]
[Authorize]
public sealed class CardFundingController : ControllerBase
{
    private readonly ICardFundingService _cardFundingService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardFundingController"/> class.
    /// </summary>
    public CardFundingController(
        ICardFundingService cardFundingService,
        ICurrentUserService currentUserService,
        IApplicationDbContext dbContext)
    {
        _cardFundingService = cardFundingService ?? throw new ArgumentNullException(nameof(cardFundingService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Initializes a secure hosted card funding checkout session.
    /// </summary>
    [HttpPost("initialize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Initialize(
        [FromBody] InitializeCardFundingApiRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return BadRequest(new { code = "INVALID_AMOUNT", message = "Funding amount must be greater than zero." });
        }

        if (string.IsNullOrWhiteSpace(request.CallbackUrl))
        {
            return BadRequest(new { code = "CALLBACK_URL_REQUIRED", message = "CallbackUrl is required." });
        }

        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var wallet = await _dbContext.Wallets.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WalletId, cancellationToken);
        if (wallet == null)
        {
            return NotFound(new { code = "WALLET_NOT_FOUND", message = $"Wallet '{request.WalletId}' not found." });
        }

        bool isAuthorized = false;
        if (wallet.IndividualId == userId)
        {
            isAuthorized = true;
        }
        else if (wallet.OrganizationId.HasValue)
        {
            var membership = await _dbContext.OrganizationMemberships.AsNoTracking()
                .FirstOrDefaultAsync(m => m.OrganizationId == wallet.OrganizationId.Value && m.UserId == userId && m.Status == MembershipStatus.Active, cancellationToken);
            if (membership != null && (membership.Role == MembershipRoleType.Owner || membership.Role == MembershipRoleType.Admin || membership.Role == MembershipRoleType.PayrollManager || membership.HasPermission(Permissions.WalletFund) || membership.HasPermission(Permissions.WalletTransfer)))
            {
                isAuthorized = true;
            }
        }

        if (!isAuthorized)
        {
            var isAdmin = await _dbContext.AdminProfiles.AsNoTracking()
                .AnyAsync(a => a.UserId == userId && !a.IsDeleted && a.IsActive, cancellationToken);
            if (isAdmin) isAuthorized = true;
        }

        if (!isAuthorized)
        {
            return Forbid();
        }

        var result = await _cardFundingService.InitializeCardFundingAsync(
            walletId: request.WalletId,
            amount: request.Amount,
            currency: request.Currency,
            provider: request.Provider,
            callbackUrl: request.CallbackUrl,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    /// Charges a tokenized saved card directly for wallet funding.
    /// </summary>
    [HttpPost("charge-saved")]
    public async Task<IActionResult> ChargeSavedCard(
        [FromBody] ChargeSavedCardApiRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKeyHeader,
        CancellationToken cancellationToken)
    {
        if (request.SavedCardId == Guid.Empty)
        {
            return BadRequest(new { code = "INVALID_SAVED_CARD", message = "SavedCardId is required." });
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new { code = "INVALID_AMOUNT", message = "Amount must be greater than zero." });
        }

        var key = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? request.IdempotencyKey
            : !string.IsNullOrWhiteSpace(idempotencyKeyHeader)
                ? idempotencyKeyHeader
                : Guid.NewGuid().ToString("N");

        var userId = _currentUserService.UserId ?? "ANONYMOUS";

        var result = await _cardFundingService.ChargeSavedCardAsync(
            savedCardId: request.SavedCardId,
            amount: request.Amount,
            currency: request.Currency,
            idempotencyKey: key,
            actorUserId: userId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    /// Reconciles the payment status of a card funding transaction against the provider gateway.
    /// </summary>
    [HttpPost("{fundingTransactionId:guid}/reconcile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reconcile(
        [FromRoute] Guid fundingTransactionId,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var fundingTx = await _dbContext.FundingTransactions.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fundingTransactionId, cancellationToken);

        var isAdmin = await _dbContext.AdminProfiles.AsNoTracking()
            .AnyAsync(a => a.UserId == userId && !a.IsDeleted && a.IsActive, cancellationToken);

        if (fundingTx == null && !isAdmin)
        {
            return NotFound(new { code = "TRANSACTION_NOT_FOUND", message = $"Funding transaction '{fundingTransactionId}' not found." });
        }

        bool isAuthorized = isAdmin;
        if (!isAuthorized && fundingTx != null)
        {
            var wallet = await _dbContext.Wallets.AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == fundingTx.WalletId, cancellationToken);

            if (wallet != null)
            {
                if (wallet.IndividualId == userId)
                {
                    isAuthorized = true;
                }
                else if (wallet.OrganizationId.HasValue)
                {
                    var isMember = await _dbContext.OrganizationMemberships.AsNoTracking()
                        .AnyAsync(m => m.OrganizationId == wallet.OrganizationId.Value && m.UserId == userId && m.Status == MembershipStatus.Active, cancellationToken);
                    if (isMember) isAuthorized = true;
                }
            }
        }

        if (!isAuthorized)
        {
            return Forbid();
        }

        var result = await _cardFundingService.ReconcileCardFundingAsync(fundingTransactionId, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}

/// <summary>
/// API request payload for initializing card funding.
/// </summary>
public sealed record InitializeCardFundingApiRequest(
    Guid WalletId,
    decimal Amount,
    Currency Currency = Currency.NGN,
    PaymentProvider? Provider = null,
    string CallbackUrl = "");

/// <summary>
/// API request payload for charging a saved card.
/// </summary>
public sealed record ChargeSavedCardApiRequest(
    Guid SavedCardId,
    decimal Amount,
    Currency Currency = Currency.NGN,
    string? IdempotencyKey = null);
