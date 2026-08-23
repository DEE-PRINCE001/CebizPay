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
/// API endpoints for organization suppliers management.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/suppliers")]
[Authorize]
public sealed class OrgSuppliersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="OrgSuppliersController"/>.
    /// </summary>
    public OrgSuppliersController(ISender sender, ICurrentOrganizationContext orgContext)
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
    /// Creates a new supplier profile.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSupplier(
        [FromBody] CreateSupplierApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new CreateSupplierCommand(
            orgId,
            request.Reference,
            request.Name,
            request.Email,
            request.Phone,
            request.Address,
            request.TaxIdentifier);

        var supplierId = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetSupplierById), new { id = supplierId }, supplierId);
    }

    /// <summary>
    /// Lists organization suppliers with search and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SupplierDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] string? search,
        [FromQuery] SupplierStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetSuppliersQuery(orgId, search, status, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets details of a single supplier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSupplierById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var query = new GetSupplierByIdQuery(id, orgId);
        var supplier = await _sender.Send(query, cancellationToken);

        if (supplier == null)
        {
            return NotFound();
        }

        return Ok(supplier);
    }

    /// <summary>
    /// Updates supplier details.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSupplier(
        [FromRoute] Guid id,
        [FromBody] UpdateSupplierApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new UpdateSupplierCommand(
            id,
            orgId,
            request.Name,
            request.Email,
            request.Phone,
            request.Address,
            request.TaxIdentifier);

        var updatedId = await _sender.Send(command, cancellationToken);
        return Ok(new { id = updatedId });
    }

    /// <summary>
    /// Soft-deletes / deactivates a supplier profile.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSupplier(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new DeleteSupplierCommand(id, orgId);
        await _sender.Send(command, cancellationToken);
        return NoContent();
    }
}
