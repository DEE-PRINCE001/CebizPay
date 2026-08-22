using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Card wallet funding initialization and reconciliation endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/funding/card")]
[Authorize]
public sealed class CardFundingController : ControllerBase
{
    private readonly ICardFundingService _cardFundingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardFundingController"/> class.
    /// </summary>
    public CardFundingController(ICardFundingService cardFundingService)
    {
        _cardFundingService = cardFundingService ?? throw new ArgumentNullException(nameof(cardFundingService));
    }

    /// <summary>
    /// Initializes a secure hosted card funding checkout session.
    /// </summary>
    [HttpPost("initialize")]
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

        var provider = request.Provider ?? PaymentProvider.Flutterwave;

        var result = await _cardFundingService.InitializeCardFundingAsync(
            walletId: request.WalletId,
            amount: request.Amount,
            currency: request.Currency,
            provider: provider,
            callbackUrl: request.CallbackUrl,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    /// Reconciles the payment status of a card funding transaction against the provider gateway.
    /// </summary>
    [HttpPost("{fundingTransactionId:guid}/reconcile")]
    public async Task<IActionResult> Reconcile(
        [FromRoute] Guid fundingTransactionId,
        CancellationToken cancellationToken)
    {
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
