using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Payments.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Zero-auth and micro-charge card verification endpoints.
/// Used to verify card ownership and securely save card tokens.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/card-verification")]
[Authorize]
public sealed class CardVerificationController : ControllerBase
{
    private readonly ICardVerificationService _verificationService;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardVerificationController"/> class.
    /// </summary>
    public CardVerificationController(
        ICardVerificationService verificationService,
        ICurrentUserService currentUserService)
    {
        _verificationService = verificationService ?? throw new ArgumentNullException(nameof(verificationService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <summary>
    /// Initializes a card verification session (zero-auth or nominal micro-charge).
    /// </summary>
    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize(
        [FromBody] InitializeCardVerificationApiRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "User is not authenticated." });
        }

        if (request.WalletId == Guid.Empty)
        {
            return BadRequest(new { code = "WALLET_REQUIRED", message = "WalletId is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { code = "EMAIL_REQUIRED", message = "Email is required." });
        }

        if (string.IsNullOrWhiteSpace(request.CallbackUrl))
        {
            return BadRequest(new { code = "CALLBACK_URL_REQUIRED", message = "CallbackUrl is required." });
        }

        var result = await _verificationService.InitializeCardVerificationAsync(
            walletId: request.WalletId,
            userId: userId,
            email: request.Email,
            callbackUrl: request.CallbackUrl,
            preferredProvider: request.PreferredProvider,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    /// Completes the card verification session and tokenizes the card.
    /// </summary>
    [HttpPost("complete")]
    public async Task<IActionResult> Complete(
        [FromBody] CompleteCardVerificationApiRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reference))
        {
            return BadRequest(new { code = "REFERENCE_REQUIRED", message = "Reference is required." });
        }

        var result = await _verificationService.CompleteCardVerificationAsync(
            reference: request.Reference,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }
}

/// <summary>
/// API request payload for initializing card verification.
/// </summary>
public sealed record InitializeCardVerificationApiRequest(
    Guid WalletId,
    string Email,
    string CallbackUrl,
    PaymentProvider? PreferredProvider = null);

/// <summary>
/// API request payload for completing card verification.
/// </summary>
public sealed record CompleteCardVerificationApiRequest(
    string Reference);
