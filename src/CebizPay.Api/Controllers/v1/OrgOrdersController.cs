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
/// API controller for ERP Purchase Orders and Sales Orders.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/orders")]
[Authorize]
public sealed class OrgOrdersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    public OrgOrdersController(ISender sender, ICurrentOrganizationContext orgContext)
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

    // ==========================================
    // Purchase Orders
    // ==========================================

    /// <summary>Creates a new draft purchase order.</summary>
    [HttpPost("purchase")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePurchaseOrder([FromBody] CreatePurchaseOrderApiRequest request, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new CreatePurchaseOrderCommand(
            orgId,
            request.SupplierId,
            request.OrderDate,
            request.ExpectedDeliveryDate,
            request.Currency,
            request.Notes,
            request.Items);

        var id = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetPurchaseOrderById), new { id }, id);
    }

    /// <summary>Retrieves paged purchase orders for the active organization.</summary>
    [HttpGet("purchase")]
    [ProducesResponseType(typeof(PagedResult<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPurchaseOrders(
        [FromQuery] PurchaseOrderStatus? status,
        [FromQuery] Guid? supplierId,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetPurchaseOrdersQuery(orgId, status, supplierId, search, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Retrieves purchase order details by ID.</summary>
    [HttpGet("purchase/{id:guid}")]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPurchaseOrderById(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var query = new GetPurchaseOrderByIdQuery(orgId, id);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Confirms a draft purchase order.</summary>
    [HttpPost("purchase/{id:guid}/confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmPurchaseOrder(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        await _sender.Send(new ConfirmPurchaseOrderCommand(orgId, id), cancellationToken);
        return Ok();
    }

    /// <summary>Receives quantities for an item line on a purchase order.</summary>
    [HttpPost("purchase/{id:guid}/items/{itemId:guid}/receive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReceivePurchaseOrderItem(
        Guid id,
        Guid itemId,
        [FromBody] ReceivePurchaseOrderItemApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new ReceivePurchaseOrderItemCommand(orgId, id, itemId, request.QuantityReceived);
        await _sender.Send(command, cancellationToken);
        return Ok();
    }

    /// <summary>Cancels a purchase order.</summary>
    [HttpPost("purchase/{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelPurchaseOrder(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        await _sender.Send(new CancelPurchaseOrderCommand(orgId, id), cancellationToken);
        return Ok();
    }

    // ==========================================
    // Sales Orders
    // ==========================================

    /// <summary>Creates a new draft sales order.</summary>
    [HttpPost("sales")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSalesOrder([FromBody] CreateSalesOrderApiRequest request, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new CreateSalesOrderCommand(
            orgId,
            request.CustomerId,
            request.OrderDate,
            request.ExpectedFulfillmentDate,
            request.Currency,
            request.Notes,
            request.Items);

        var id = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetSalesOrderById), new { id }, id);
    }

    /// <summary>Retrieves paged sales orders for the active organization.</summary>
    [HttpGet("sales")]
    [ProducesResponseType(typeof(PagedResult<SalesOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalesOrders(
        [FromQuery] SalesOrderStatus? status,
        [FromQuery] Guid? customerId,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetSalesOrdersQuery(orgId, status, customerId, search, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Retrieves sales order details by ID.</summary>
    [HttpGet("sales/{id:guid}")]
    [ProducesResponseType(typeof(SalesOrderDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalesOrderById(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var query = new GetSalesOrderByIdQuery(orgId, id);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Confirms a draft sales order.</summary>
    [HttpPost("sales/{id:guid}/confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmSalesOrder(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        await _sender.Send(new ConfirmSalesOrderCommand(orgId, id), cancellationToken);
        return Ok();
    }

    /// <summary>Fulfills quantities for an item line on a sales order.</summary>
    [HttpPost("sales/{id:guid}/items/{itemId:guid}/fulfill")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> FulfillSalesOrderItem(
        Guid id,
        Guid itemId,
        [FromBody] FulfillSalesOrderItemApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new FulfillSalesOrderItemCommand(orgId, id, itemId, request.QuantityFulfilled);
        await _sender.Send(command, cancellationToken);
        return Ok();
    }

    /// <summary>Cancels a sales order.</summary>
    [HttpPost("sales/{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelSalesOrder(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        await _sender.Send(new CancelSalesOrderCommand(orgId, id), cancellationToken);
        return Ok();
    }
}
