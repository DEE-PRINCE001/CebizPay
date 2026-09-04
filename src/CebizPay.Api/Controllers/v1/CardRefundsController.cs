using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Application.Common.Interfaces.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Card refund management endpoints.
/// Handles provider refund execution and central ledger reversals.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/card-refunds")]
[Authorize]
public sealed class CardRefundsController : ControllerBase
{
    private readonly ICardRefundService _cardRefundService;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CardRefundsController"/> class.
    /// </summary>
    public CardRefundsController(
        ICardRefundService cardRefundService,
        ICurrentUserService currentUserService)
    {
        _cardRefundService = cardRefundService ?? throw new ArgumentNullException(nameof(cardRefundService));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <summary>
    /// Requests a refund for a completed card funding transaction.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RequestRefund(
        [FromBody] RequestCardRefundApiRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKeyHeader,
        CancellationToken cancellationToken)
    {
        if (request.FundingTransactionId == Guid.Empty)
        {
            return BadRequest(new { code = "INVALID_FUNDING_TRANSACTION", message = "FundingTransactionId is required." });
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new { code = "INVALID_AMOUNT", message = "Refund amount must be greater than zero." });
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new { code = "REASON_REQUIRED", message = "Reason is required." });
        }

        var key = !string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? request.IdempotencyKey
            : !string.IsNullOrWhiteSpace(idempotencyKeyHeader)
                ? idempotencyKeyHeader
                : Guid.NewGuid().ToString("N");

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";

        var result = await _cardRefundService.RequestCardRefundAsync(
            fundingTransactionId: request.FundingTransactionId,
            amount: request.Amount,
            reason: request.Reason,
            idempotencyKey: key,
            actorUserId: actorUserId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    /// Retrieves a specific card refund by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRefundById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var refund = await _cardRefundService.GetRefundByIdAsync(id, actorUserId, cancellationToken).ConfigureAwait(false);
        if (refund == null)
        {
            return NotFound(new { code = "REFUND_NOT_FOUND", message = $"Card refund '{id}' not found." });
        }

        return Ok(refund);
    }

    /// <summary>
    /// Reconciles or re-attempts ledger reversal for a card refund.
    /// </summary>
    [HttpPost("{id:guid}/reconcile")]
    [Authorize(Policy = CebizPay.Application.Common.Security.AuthorizationPolicies.RequirePlatformAdmin)]
    public async Task<IActionResult> ReconcileRefund(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _cardRefundService.ReconcileRefundAsync(id, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}

/// <summary>
/// API request payload for requesting a card refund.
/// </summary>
public sealed record RequestCardRefundApiRequest(
    Guid FundingTransactionId,
    decimal Amount,
    string Reason,
    string? IdempotencyKey = null);
