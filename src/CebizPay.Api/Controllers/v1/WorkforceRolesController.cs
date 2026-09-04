using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Organizations.Workforce;
using CebizPay.Domain.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API endpoints for managing organization workforce roles.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/roles")]
[Authorize]
public sealed class WorkforceRolesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="WorkforceRolesController"/>.
    /// </summary>
    public WorkforceRolesController(ISender sender, ICurrentOrganizationContext orgContext)
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
    /// Lists all workforce roles for the organization with optional department filter, pagination, and search.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<WorkforceRoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRoles(
        [FromQuery] Guid? departmentId,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.StaffView, cancellationToken))
        {
            return Forbid();
        }

        var query = new GetWorkforceRolesQuery(orgId, departmentId, search, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets a single workforce role by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkforceRoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRoleById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.StaffView, cancellationToken))
        {
            return Forbid();
        }

        var query = new GetWorkforceRoleByIdQuery(id, orgId);
        var result = await _sender.Send(query, cancellationToken);
        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Role Not Found",
                Detail = $"Workforce role '{id}' was not found in this organization."
            });
        }
        return Ok(result);
    }

    /// <summary>
    /// Creates a new workforce role in the organization.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateRole(
        [FromBody] CreateWorkforceRoleApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.RolesManage, cancellationToken))
        {
            return Forbid();
        }

        var command = new CreateWorkforceRoleCommand(orgId, request.Title, request.DepartmentId, request.Description);
        var id = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetRoleById), new { version = "1.0", id }, new { id });
    }

    /// <summary>
    /// Updates an existing workforce role in the organization.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateRole(
        [FromRoute] Guid id,
        [FromBody] UpdateWorkforceRoleApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.RolesManage, cancellationToken))
        {
            return Forbid();
        }

        var command = new UpdateWorkforceRoleCommand(id, orgId, request.Title, request.DepartmentId, request.Description);
        var resultId = await _sender.Send(command, cancellationToken);
        return Ok(new { id = resultId });
    }

    /// <summary>
    /// Deletes a workforce role from the organization.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteRole(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.RolesManage, cancellationToken))
        {
            return Forbid();
        }

        var command = new DeleteWorkforceRoleCommand(id, orgId);
        await _sender.Send(command, cancellationToken);
        return NoContent();
    }
}

/// <summary>Request payload for creating a workforce role.</summary>
public sealed record CreateWorkforceRoleApiRequest(string Title, Guid? DepartmentId, string? Description);

/// <summary>Request payload for updating a workforce role.</summary>
public sealed record UpdateWorkforceRoleApiRequest(string Title, Guid? DepartmentId, string? Description);
