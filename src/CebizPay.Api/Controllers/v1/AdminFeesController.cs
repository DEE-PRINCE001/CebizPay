using System.Security.Claims;
using Asp.Versioning;
using CebizPay.Application.UseCases.Admin.Fees;
using CebizPay.Domain.Finance.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Platform fee policy management endpoints.
/// Only authorized Super Admin users may modify fee policies.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/fees")]
[Authorize]
public sealed class AdminFeesController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminFeesController"/>.
    /// </summary>
    public AdminFeesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Returns the currently active peer-transfer fee policy.
    /// </summary>
    [HttpGet("peer-transfer/active")]
    public async Task<IActionResult> GetActivePolicy(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetActiveFeePolicyQuery(), cancellationToken);
        if (result == null)
            return NotFound(new { message = "No active peer-transfer fee policy is configured." });

        return Ok(result);
    }

    /// <summary>
    /// Returns all historical peer-transfer fee policies ordered by version descending.
    /// </summary>
    [HttpGet("peer-transfer")]
    public async Task<IActionResult> GetAllPolicies(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetAllFeePoliciesQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates and activates a new peer-transfer fee policy. Deactivates the previous active policy.
    /// Super Admin only — authorization is enforced within the command handler.
    /// Every policy change is audit-logged.
    /// </summary>
    [HttpPost("peer-transfer")]
    public async Task<IActionResult> CreatePolicy(
        [FromBody] CreateFeePolicyRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var command = new CreateFeePolicyCommand(
            Mode: request.Mode,
            PercentageRate: request.PercentageRate,
            MinimumFee: request.MinimumFee,
            MaximumFee: request.MaximumFee,
            CreatedByUserId: userId);

        var result = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetActivePolicy), null, result);
    }

    /// <summary>
    /// Returns the currently active bank-transfer fee policy.
    /// </summary>
    [HttpGet("bank-transfer/active")]
    public async Task<IActionResult> GetActiveBankTransferPolicy(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetActiveBankTransferFeePolicyQuery(), cancellationToken);
        if (result == null)
            return NotFound(new { message = "No active bank-transfer fee policy is configured." });

        return Ok(result);
    }

    /// <summary>
    /// Returns all historical bank-transfer fee policies ordered by version descending.
    /// </summary>
    [HttpGet("bank-transfer")]
    public async Task<IActionResult> GetAllBankTransferPolicies(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetAllBankTransferFeePoliciesQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates and activates a new bank-transfer fee policy. Deactivates the previous active policy.
    /// Super Admin only — authorization is enforced within the command handler.
    /// Every policy change is audit-logged.
    /// </summary>
    [HttpPost("bank-transfer")]
    public async Task<IActionResult> CreateBankTransferPolicy(
        [FromBody] CreateBankTransferFeePolicyRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var command = new CreateBankTransferFeePolicyCommand(
            Mode: request.Mode,
            PercentageRate: request.PercentageRate,
            MinimumFee: request.MinimumFee,
            MaximumFee: request.MaximumFee,
            CreatedByUserId: userId);

        var result = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetActiveBankTransferPolicy), null, result);
    }
}

/// <summary>
/// Request body DTO for creating a peer-transfer fee policy.
/// </summary>
/// <param name="Mode">Fee mode: Free or Percentage.</param>
/// <param name="PercentageRate">Decimal rate (e.g. 0.01 = 1%). Required for Percentage mode.</param>
/// <param name="MinimumFee">Minimum fee floor. Required for Percentage mode.</param>
/// <param name="MaximumFee">Maximum fee ceiling. Required for Percentage mode.</param>
public sealed record CreateFeePolicyRequest(
    FeePolicyMode Mode,
    decimal? PercentageRate = null,
    decimal? MinimumFee = null,
    decimal? MaximumFee = null);

/// <summary>
/// Request body DTO for creating a bank-transfer fee policy.
/// </summary>
/// <param name="Mode">Fee mode: Free or Percentage.</param>
/// <param name="PercentageRate">Decimal rate (e.g. 0.015 = 1.5%). Required for Percentage mode.</param>
/// <param name="MinimumFee">Minimum fee floor. Required for Percentage mode.</param>
/// <param name="MaximumFee">Maximum fee ceiling. Required for Percentage mode.</param>
public sealed record CreateBankTransferFeePolicyRequest(
    FeePolicyMode Mode,
    decimal? PercentageRate = null,
    decimal? MinimumFee = null,
    decimal? MaximumFee = null);

