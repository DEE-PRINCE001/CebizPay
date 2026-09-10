using Asp.Versioning;
using CebizPay.Application.UseCases.Admin.Treasury;
using CebizPay.Domain.Finance.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Platform Master Wallet and Treasury Controller.
/// Provides platform fee collection pool balances, escrow holdings, and settlement liquidity state.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/treasury")]
[Authorize(Roles = "Admin,SuperAdmin")]
public sealed class AdminTreasuryController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminTreasuryController"/>.
    /// </summary>
    public AdminTreasuryController(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <summary>
    /// Retrieves the platform master wallet / treasury summary.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(PlatformTreasurySummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] Currency currency = Currency.NGN,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetPlatformTreasurySummaryQuery(currency), cancellationToken);
        return Ok(result);
    }
}
