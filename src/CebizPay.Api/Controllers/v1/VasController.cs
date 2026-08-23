using Asp.Versioning;
using CebizPay.Application.Common.Models.Vas;
using CebizPay.Application.UseCases.Vas.Commands.PurchaseAirtime;
using CebizPay.Application.UseCases.Vas.Commands.PurchaseData;
using CebizPay.Application.UseCases.Vas.Queries.DetectOperator;
using CebizPay.Application.UseCases.Vas.Queries.GetDataBundles;
using CebizPay.Application.UseCases.Vas.Queries.GetVasTransactionById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API endpoints for Value-Added Services (VAS) including Airtime top-up, Data bundle purchases,
/// Operator detection, and Bundle plan lookups.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/vas")]
[Authorize]
public sealed class VasController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="VasController"/>.
    /// </summary>
    public VasController(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <summary>
    /// Executes an airtime top-up purchase for a Nigerian phone number.
    /// Deducts amount from customer wallet and fulfills airtime via VTUGATE.
    /// Protected by a 120-second duplicate purchase prevention window.
    /// </summary>
    /// <param name="request">Airtime purchase payload.</param>
    /// <param name="idempotencyKeyHeader">Unique idempotency key from HTTP header.</param>
    /// <param name="organizationIdHeader">Optional organization context header.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("airtime")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("FinancialTransferPolicy")]
    [ProducesResponseType(typeof(VasPurchaseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PurchaseAirtime(
        [FromBody] PurchaseAirtimeApiRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyHeader,
        [FromHeader(Name = "X-Organization-Id")] Guid? organizationIdHeader,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = idempotencyKeyHeader ?? request.IdempotencyKey;
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new { code = "IDEMPOTENCY_KEY_REQUIRED", message = "Idempotency-Key header or idempotencyKey body field is required." });
        }

        var orgContext = organizationIdHeader ?? request.OrganizationContext;

        var command = new PurchaseAirtimeCommand(
            PhoneNumber: request.PhoneNumber,
            Network: request.Network,
            Amount: request.Amount,
            TransactionPin: request.TransactionPin,
            IdempotencyKey: idempotencyKey,
            OrganizationContext: orgContext);

        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Executes a mobile data bundle purchase for a Nigerian phone number.
    /// Deducts plan amount from customer wallet and fulfills data bundle via VTUGATE.
    /// Protected by a 120-second duplicate purchase prevention window.
    /// </summary>
    /// <param name="request">Data bundle purchase payload.</param>
    /// <param name="idempotencyKeyHeader">Unique idempotency key from HTTP header.</param>
    /// <param name="organizationIdHeader">Optional organization context header.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("data")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("FinancialTransferPolicy")]
    [ProducesResponseType(typeof(VasPurchaseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PurchaseData(
        [FromBody] PurchaseDataApiRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyHeader,
        [FromHeader(Name = "X-Organization-Id")] Guid? organizationIdHeader,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = idempotencyKeyHeader ?? request.IdempotencyKey;
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new { code = "IDEMPOTENCY_KEY_REQUIRED", message = "Idempotency-Key header or idempotencyKey body field is required." });
        }

        var orgContext = organizationIdHeader ?? request.OrganizationContext;

        var command = new PurchaseDataCommand(
            PhoneNumber: request.PhoneNumber,
            Network: request.Network,
            ProductCode: request.ProductCode,
            Amount: request.Amount,
            TransactionPin: request.TransactionPin,
            IdempotencyKey: idempotencyKey,
            OrganizationContext: orgContext);

        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves catalog of available mobile data bundle plans, optionally filtered by operator.
    /// </summary>
    /// <param name="network">Optional telecommunication network (MTN, AIRTEL, GLO, 9MOBILE).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("data/bundles")]
    [ProducesResponseType(typeof(IReadOnlyList<DataBundleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDataBundles(
        [FromQuery] string? network,
        CancellationToken cancellationToken)
    {
        var query = new GetDataBundlesQuery(network);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Automatically detects mobile telecommunication network operator for a given Nigerian phone number.
    /// </summary>
    /// <param name="phoneNumber">Recipient phone number in national or international format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("operators/detect")]
    [ProducesResponseType(typeof(OperatorDetectionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DetectOperator(
        [FromQuery] string phoneNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return BadRequest(new { code = "PHONE_NUMBER_REQUIRED", message = "phoneNumber query parameter is required." });
        }

        var query = new DetectOperatorQuery(phoneNumber);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves details and current status of a VAS purchase transaction by ID.
    /// Enforces multi-tenant and personal ownership boundaries.
    /// </summary>
    /// <param name="id">VAS transaction unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("transactions/{id:guid}")]
    [ProducesResponseType(typeof(VasTransactionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetVasTransactionById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetVasTransactionByIdQuery(id);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }
}

/// <summary>Airtime purchase HTTP request payload.</summary>
public sealed record PurchaseAirtimeApiRequest(
    string PhoneNumber,
    string Network,
    decimal Amount,
    string TransactionPin,
    string? IdempotencyKey = null,
    Guid? OrganizationContext = null);

/// <summary>Data bundle purchase HTTP request payload.</summary>
public sealed record PurchaseDataApiRequest(
    string PhoneNumber,
    string Network,
    string ProductCode,
    decimal Amount,
    string TransactionPin,
    string? IdempotencyKey = null,
    Guid? OrganizationContext = null);
