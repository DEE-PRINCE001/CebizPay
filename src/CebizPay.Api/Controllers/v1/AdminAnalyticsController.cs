using Asp.Versioning;
using CebizPay.Application.UseCases.Admin.Analytics;
using CebizPay.Domain.Finance.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Platform Administrative Analytics Controller.
/// Powers platform revenue and transaction volume time series charts.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/analytics")]
[Authorize(Roles = "Admin,SuperAdmin")]
public sealed class AdminAnalyticsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminAnalyticsController"/>.
    /// </summary>
    public AdminAnalyticsController(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <summary>
    /// Retrieves yearly platform fee revenue, total transaction volume, MoM growth rate, and monthly data time series.
    /// </summary>
    [HttpGet("revenue")]
    [ProducesResponseType(typeof(PlatformRevenueAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRevenueAnalytics(
        [FromQuery] int? year = null,
        [FromQuery] Currency currency = Currency.NGN,
        CancellationToken cancellationToken = default)
    {
        var targetYear = year ?? DateTime.UtcNow.Year;
        var result = await _sender.Send(new GetPlatformRevenueAnalyticsQuery(targetYear, currency), cancellationToken);
        return Ok(result);
    }
}
