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
/// API endpoints for organization billable service offerings catalog.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/services")]
[Authorize]
public sealed class OrgServicesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="OrgServicesController"/>.
    /// </summary>
    public OrgServicesController(ISender sender, ICurrentOrganizationContext orgContext)
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
    /// Creates a new service offering in the organization catalog.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateService(
        [FromBody] CreateErpServiceApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new CreateErpServiceCommand(
            orgId,
            request.Code,
            request.Name,
            request.UnitPrice,
            request.Description,
            request.Currency);

        var serviceId = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetServiceById), new { id = serviceId }, serviceId);
    }

    /// <summary>
    /// Lists organization services with search and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ErpServiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServices(
        [FromQuery] string? search,
        [FromQuery] ErpServiceStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetErpServicesQuery(orgId, search, status, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets details of a single service offering.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ErpServiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServiceById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var query = new GetErpServiceByIdQuery(id, orgId);
        var service = await _sender.Send(query, cancellationToken);

        if (service == null)
        {
            return NotFound();
        }

        return Ok(service);
    }

    /// <summary>
    /// Updates service metadata and unit price.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateService(
        [FromRoute] Guid id,
        [FromBody] UpdateErpServiceApiRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new UpdateErpServiceCommand(
            id,
            orgId,
            request.Name,
            request.UnitPrice,
            request.Description);

        var updatedId = await _sender.Send(command, cancellationToken);
        return Ok(new { id = updatedId });
    }

    /// <summary>
    /// Soft-deletes / deactivates a service offering.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteService(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var command = new DeleteErpServiceCommand(id, orgId);
        await _sender.Send(command, cancellationToken);
        return NoContent();
    }
}
