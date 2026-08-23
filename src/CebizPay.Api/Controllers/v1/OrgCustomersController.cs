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
/// API endpoints for organization customers management.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/customers")]
[Authorize]
public sealed class OrgCustomersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="OrgCustomersController"/>.
    /// </summary>
    public OrgCustomersController(ISender sender, ICurrentOrganizationContext orgContext)
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
    /// Creates a new customer profile.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCustomer(
        [FromBody] CreateCustomerApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new CreateCustomerCommand(
            orgId,
            request.Reference,
            request.Name,
            request.Email,
            request.Phone,
            request.Address);

        var customerId = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetCustomerById), new { id = customerId }, customerId);
    }

    /// <summary>
    /// Lists organization customers with search and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CustomerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] string? search,
        [FromQuery] CustomerStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetCustomersQuery(orgId, search, status, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets details of a single customer.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustomerById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var query = new GetCustomerByIdQuery(id, orgId);
        var customer = await _sender.Send(query, cancellationToken);

        if (customer == null)
        {
            return NotFound();
        }

        return Ok(customer);
    }

    /// <summary>
    /// Updates customer details.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCustomer(
        [FromRoute] Guid id,
        [FromBody] UpdateCustomerApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new UpdateCustomerCommand(
            id,
            orgId,
            request.Name,
            request.Email,
            request.Phone,
            request.Address);

        var updatedId = await _sender.Send(command, cancellationToken);
        return Ok(new { id = updatedId });
    }

    /// <summary>
    /// Soft-deletes / deactivates a customer profile.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCustomer(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new DeleteCustomerCommand(id, orgId);
        await _sender.Send(command, cancellationToken);
        return NoContent();
    }
}
