#pragma warning disable CS1591
using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Organizations.Erp;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API controller for ERP Payment Receipts.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/receipts")]
[Authorize]
public sealed class OrgReceiptsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    public OrgReceiptsController(ISender sender, ICurrentOrganizationContext orgContext)
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

    /// <summary>Retrieves paged receipts for the active organization.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ErpReceiptDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceipts(
        [FromQuery] Guid? customerId,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetReceiptsQuery(orgId, customerId, search, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Retrieves receipt details by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ErpReceiptDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceiptById(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var query = new GetReceiptByIdQuery(orgId, id);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Retrieves receipt details by invoice ID.</summary>
    [HttpGet("by-invoice/{invoiceId:guid}")]
    [ProducesResponseType(typeof(ErpReceiptDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceiptByInvoiceId(Guid invoiceId, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var query = new GetReceiptByInvoiceIdQuery(orgId, invoiceId);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }
}
