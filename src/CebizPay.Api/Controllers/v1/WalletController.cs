using Asp.Versioning;
using CebizPay.Application.UseCases.Wallet.Transfer;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Wallet operations endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/wallet")]
[Authorize]
public sealed class WalletController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="WalletController"/>.
    /// </summary>
    public WalletController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Executes a peer wallet transfer from the authenticated user's wallet to another CebizPay user's wallet.
    ///
    /// The sender's identity is resolved from the JWT bearer token — do NOT supply sender identity fields.
    /// The canonical Idempotency-Key header (or idempotencyKey body field) must be unique per logical transfer.
    /// Repeated requests with the same key and identical payload return the original result without re-executing.
    /// </summary>
    /// <param name="request">Transfer request body.</param>
    /// <param name="idempotencyKeyHeader">Idempotency key from Idempotency-Key header (optional; falls back to body field).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("transfer/peer")]
    public async Task<IActionResult> PeerTransfer(
        [FromBody] PeerTransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyHeader,
        CancellationToken cancellationToken)
    {
        // Idempotency key: header takes precedence over body field
        var idempotencyKey = idempotencyKeyHeader ?? request.IdempotencyKey;

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new { code = "IDEMPOTENCY_KEY_REQUIRED", message = "Idempotency-Key header or idempotencyKey body field is required." });
        }

        var command = new PeerTransferCommand(
            RecipientIdentifier: request.RecipientIdentifier,
            Amount: request.Amount,
            Currency: request.Currency,
            TransactionPin: request.TransactionPin,
            IdempotencyKey: idempotencyKey,
            OrganizationContext: request.OrganizationContext);

        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }
}

/// <summary>
/// Request body DTO for peer wallet transfer.
/// </summary>
/// <param name="RecipientIdentifier">Email or phone number of the recipient CebizPay user.</param>
/// <param name="Amount">Transfer amount (positive decimal).</param>
/// <param name="Currency">V1 transactional currency: NGN, INTERNATIONAL_NGN, or USDT.</param>
/// <param name="TransactionPin">4-digit numeric transaction PIN.</param>
/// <param name="IdempotencyKey">Client-supplied idempotency key (also accepted as Idempotency-Key header).</param>
/// <param name="OrganizationContext">
/// Optional organization ID. If provided, the transfer is from the organization's wallet.
/// If null, the transfer is from the user's personal wallet.
/// </param>
public sealed record PeerTransferRequest(
    string RecipientIdentifier,
    decimal Amount,
    string Currency,
    string TransactionPin,
    string? IdempotencyKey = null,
    Guid? OrganizationContext = null);
