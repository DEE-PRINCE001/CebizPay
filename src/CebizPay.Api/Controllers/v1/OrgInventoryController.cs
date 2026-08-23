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
/// API endpoints for organization inventory items, stock movements, and valuation policies.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/inventory")]
[Authorize]
public sealed class OrgInventoryController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="OrgInventoryController"/>.
    /// </summary>
    public OrgInventoryController(ISender sender, ICurrentOrganizationContext orgContext)
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

    /// <summary>
    /// Creates a new inventory item in the organization.
    /// </summary>
    [HttpPost("items")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateItem(
        [FromBody] CreateInventoryItemApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new CreateInventoryItemCommand(
            orgId,
            request.Sku,
            request.Name,
            request.UnitOfMeasure,
            request.SellingPrice,
            request.Description,
            request.Category,
            request.ReorderLevel,
            request.Currency,
            request.InitialQuantity,
            request.InitialUnitCost);

        var itemId = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetItemById), new { id = itemId }, itemId);
    }

    /// <summary>
    /// Lists inventory items with search, filter, and pagination.
    /// </summary>
    [HttpGet("items")]
    [ProducesResponseType(typeof(PagedResult<InventoryItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItems(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] StockStatus? stockStatus,
        [FromQuery] InventoryItemStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetInventoryItemsQuery(orgId, search, category, stockStatus, status, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets details of a single inventory item.
    /// </summary>
    [HttpGet("items/{id:guid}")]
    [ProducesResponseType(typeof(InventoryItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetItemById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var query = new GetInventoryItemByIdQuery(id, orgId);
        var item = await _sender.Send(query, cancellationToken);

        if (item == null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    /// <summary>
    /// Updates inventory item details.
    /// </summary>
    [HttpPut("items/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateItem(
        [FromRoute] Guid id,
        [FromBody] UpdateInventoryItemApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new UpdateInventoryItemCommand(
            id,
            orgId,
            request.Name,
            request.UnitOfMeasure,
            request.SellingPrice,
            request.Description,
            request.Category,
            request.ReorderLevel);

        var updatedId = await _sender.Send(command, cancellationToken);
        return Ok(new { id = updatedId });
    }

    /// <summary>
    /// Soft-deletes / deactivates an inventory item.
    /// </summary>
    [HttpDelete("items/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteItem(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new DeleteInventoryItemCommand(id, orgId);
        await _sender.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Receives incoming stock into inventory.
    /// </summary>
    [HttpPost("items/{id:guid}/stock-in")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> StockIn(
        [FromRoute] Guid id,
        [FromBody] StockInApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new StockInCommand(
            id,
            orgId,
            request.Quantity,
            request.UnitCost,
            request.Reference,
            request.Reason);

        var movementId = await _sender.Send(command, cancellationToken);
        return Ok(new { movementId });
    }

    /// <summary>
    /// Issues outgoing stock from inventory.
    /// </summary>
    [HttpPost("items/{id:guid}/stock-out")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> StockOut(
        [FromRoute] Guid id,
        [FromBody] StockOutApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new StockOutCommand(
            id,
            orgId,
            request.Quantity,
            request.Reference,
            request.Reason);

        var movementId = await _sender.Send(command, cancellationToken);
        return Ok(new { movementId });
    }

    /// <summary>
    /// Manually adjusts inventory stock quantity.
    /// </summary>
    [HttpPost("items/{id:guid}/adjust")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdjustStock(
        [FromRoute] Guid id,
        [FromBody] StockAdjustmentApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new StockAdjustmentCommand(
            id,
            orgId,
            request.QuantityDelta,
            request.Reference,
            request.Reason,
            request.NewAverageCost);

        var movementId = await _sender.Send(command, cancellationToken);
        return Ok(new { movementId });
    }

    /// <summary>
    /// Lists stock movements for an inventory item.
    /// </summary>
    [HttpGet("items/{id:guid}/movements")]
    [ProducesResponseType(typeof(PagedResult<StockMovementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovements(
        [FromRoute] Guid id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetStockMovementsQuery(id, orgId, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets the current active inventory valuation policy for the organization.
    /// </summary>
    [HttpGet("valuation-policy")]
    [ProducesResponseType(typeof(InventoryValuationPolicyDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetValuationPolicy(CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var query = new GetValuationPolicyQuery(orgId);
        var policy = await _sender.Send(query, cancellationToken);
        return Ok(policy);
    }

    /// <summary>
    /// Configures or changes the organization inventory valuation method (WAC / FIFO).
    /// </summary>
    [HttpPost("valuation-policy")]
    [ProducesResponseType(typeof(InventoryValuationPolicyDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetValuationPolicy(
        [FromBody] SetValuationPolicyApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new SetValuationPolicyCommand(orgId, request.Method);
        var policy = await _sender.Send(command, cancellationToken);
        return Ok(policy);
    }
}
