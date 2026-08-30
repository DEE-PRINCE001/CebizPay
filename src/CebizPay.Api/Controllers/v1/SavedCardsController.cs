using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Application.Common.Interfaces.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Tokenized saved card management endpoints.
/// Users can list their cards, view card details (last 4 digits only), set default cards, and revoke cards.
/// Raw PAN and CVV are never accepted or stored.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/saved-cards")]
[Authorize]
public sealed class SavedCardsController : ControllerBase
{
    private readonly ISavedCardService _savedCardService;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SavedCardsController"/> class.
    /// </summary>
    public SavedCardsController(
        ISavedCardService savedCardService,
        ICurrentUserService currentUserService)
    {
        _savedCardService = savedCardService ?? throw new ArgumentNullException(nameof(savedCardService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <summary>
    /// Retrieves all active saved cards for the authenticated user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSavedCards(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "User is not authenticated." });
        }

        var cards = await _savedCardService.GetSavedCardsForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(cards);
    }

    /// <summary>
    /// Retrieves a specific saved card by ID for the authenticated user.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSavedCardById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "User is not authenticated." });
        }

        var card = await _savedCardService.GetSavedCardByIdAsync(id, userId, cancellationToken).ConfigureAwait(false);
        if (card == null)
        {
            return NotFound(new { code = "CARD_NOT_FOUND", message = $"Saved card '{id}' not found." });
        }

        return Ok(card);
    }

    /// <summary>
    /// Sets a saved card as the default card for wallet funding.
    /// </summary>
    [HttpPost("{id:guid}/default")]
    public async Task<IActionResult> SetDefaultCard(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "User is not authenticated." });
        }

        var card = await _savedCardService.SetDefaultCardAsync(id, userId, cancellationToken).ConfigureAwait(false);
        return Ok(card);
    }

    /// <summary>
    /// Revokes/deletes a saved card token.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RevokeCard(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { code = "UNAUTHORIZED", message = "User is not authenticated." });
        }

        var card = await _savedCardService.RevokeSavedCardAsync(id, userId, cancellationToken).ConfigureAwait(false);
        return Ok(card);
    }
}
