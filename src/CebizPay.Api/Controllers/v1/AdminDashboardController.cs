using Asp.Versioning;
using CebizPay.Application.UseCases.Admin.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Platform Administrative Dashboard Controller.
/// Supplies consolidated metrics and KPI card aggregates for executive administration.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/dashboard")]
[Authorize(Roles = "Admin,SuperAdmin")]
public sealed class AdminDashboardController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminDashboardController"/>.
    /// </summary>
    public AdminDashboardController(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <summary>
    /// Retrieves the platform KPI metrics summary for dashboard cards.
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(PlatformAdminMetricsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMetrics(CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetPlatformAdminMetricsQuery(), cancellationToken);
        return Ok(result);
    }
}
