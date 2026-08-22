using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Payroll;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Super Admin read-only administrative payroll analytics endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/organizations/{id:guid}/payroll-analytics")]
[Authorize]
public sealed class AdminPayrollController : ControllerBase
{
    private readonly IPayrollBatchService _batchService;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminPayrollController"/>.
    /// </summary>
    public AdminPayrollController(IPayrollBatchService batchService)
    {
        _batchService = batchService ?? throw new ArgumentNullException(nameof(batchService));
    }

    /// <summary>
    /// Retrieves aggregated multi-currency payroll analytics for an organization.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPayrollAnalytics(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var analytics = await _batchService.GetOrganizationPayrollAnalyticsAsync(id, cancellationToken).ConfigureAwait(false);
        return Ok(analytics);
    }
}
