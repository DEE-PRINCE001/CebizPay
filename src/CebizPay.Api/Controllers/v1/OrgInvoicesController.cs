#pragma warning disable CS1591
using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Organizations.Erp;
using CebizPay.Domain.Erp.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API controller for ERP Invoicing.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/invoices")]
[Authorize]
public sealed class OrgInvoicesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    public OrgInvoicesController(ISender sender, ICurrentOrganizationContext orgContext)
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

    /// <summary>Creates a new ERP invoice (calculates 7.5% statutory VAT if ApplyVat = true).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceApiRequest request, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new CreateInvoiceCommand(
            orgId,
            request.CustomerId,
            request.IssueDate,
            request.DueDate,
            request.ApplyVat,
            request.SalesOrderId,
            request.Currency,
            request.Notes,
            request.BillingContact,
            request.Items);

        var id = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetInvoiceById), new { id }, id);
    }

    /// <summary>Retrieves paged invoices for the active organization.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ErpInvoiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] InvoiceStatus? status,
        [FromQuery] Guid? customerId,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetInvoicesQuery(orgId, status, customerId, search, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Retrieves invoice details by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ErpInvoiceDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoiceById(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var query = new GetInvoiceByIdQuery(orgId, id);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Issues a draft invoice to the customer.</summary>
    [HttpPost("{id:guid}/issue")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> IssueInvoice(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        await _sender.Send(new IssueInvoiceCommand(orgId, id), cancellationToken);
        return Ok();
    }

    /// <summary>Records an invoice payment / settlement (generates immutable receipt atomically when fully paid).</summary>
    [HttpPost("{id:guid}/payments")]
    [ProducesResponseType(typeof(Guid?), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordPayment(
        Guid id,
        [FromBody] RecordInvoicePaymentApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new RecordInvoicePaymentCommand(
            orgId,
            id,
            request.Amount,
            request.SettlementMethod,
            request.Reference,
            request.Pin,
            request.IdempotencyKey);

        var receiptId = await _sender.Send(command, cancellationToken);
        return Ok(new { ReceiptId = receiptId });
    }

    /// <summary>Cancels an invoice.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelInvoice(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        await _sender.Send(new CancelInvoiceCommand(orgId, id), cancellationToken);
        return Ok();
    }
}
