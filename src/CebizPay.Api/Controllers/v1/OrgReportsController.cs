#pragma warning disable CS1591
using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Organizations.Erp;
using CebizPay.Domain.Finance.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API controller for ERP Financial and Operational Reports.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/reports")]
[Authorize]
public sealed class OrgReportsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    public OrgReportsController(ISender sender, ICurrentOrganizationContext orgContext)
    {
        _sender = sender;
        _orgContext = orgContext;
    }

    private Guid GetOrganizationId()
    {
        var orgId = _orgContext.CurrentOrganizationId;
        if (!orgId.HasValue || orgId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Active organization context is required.");
        }
        return orgId.Value;
    }

    /// <summary>Generates the organization sales report.</summary>
    [HttpGet("sales")]
    [ProducesResponseType(typeof(SalesReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalesReport(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] Guid? customerId,
        [FromQuery] Currency? currency,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var query = new GetSalesReportQuery(orgId, fromUtc, toUtc, customerId, currency);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Generates the organization purchase report.</summary>
    [HttpGet("purchases")]
    [ProducesResponseType(typeof(PurchaseReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPurchaseReport(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] Guid? supplierId,
        [FromQuery] Currency? currency,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var query = new GetPurchaseReportQuery(orgId, fromUtc, toUtc, supplierId, currency);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Generates the organization financial settlement report.</summary>
    [HttpGet("settlements")]
    [ProducesResponseType(typeof(SettlementReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettlementReport(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] Currency? currency,
        [FromQuery] string? settlementMethod,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetSettlementReportQuery(orgId, fromUtc, toUtc, currency, settlementMethod, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Generates the organization Profit &amp; Loss report.</summary>
    [HttpGet("profit-loss")]
    [ProducesResponseType(typeof(ProfitLossReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfitLossReport(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] Currency? currency,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetProfitLossReportQuery(orgId, fromUtc, toUtc, currency);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }
}
