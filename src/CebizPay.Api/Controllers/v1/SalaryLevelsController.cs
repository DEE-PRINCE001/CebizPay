using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Organizations.Workforce;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API endpoints for managing organization salary levels.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/levels")]
[Authorize]
public sealed class SalaryLevelsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="SalaryLevelsController"/>.
    /// </summary>
    public SalaryLevelsController(ISender sender, ICurrentOrganizationContext orgContext)
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
    /// Lists all salary levels for the organization with optional currency filter, pagination, and search.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SalaryLevelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalaryLevels(
        [FromQuery] string? currency,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetSalaryLevelsQuery(orgId, currency, search, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets a single salary level by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SalaryLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSalaryLevelById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetSalaryLevelByIdQuery(id, orgId);
        var result = await _sender.Send(query, cancellationToken);
        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Salary Level Not Found",
                Detail = $"Salary level '{id}' was not found in this organization."
            });
        }
        return Ok(result);
    }

    /// <summary>
    /// Creates a new salary level in the organization.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSalaryLevel(
        [FromBody] CreateSalaryLevelApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var command = new CreateSalaryLevelCommand(orgId, request.LevelName, request.BaseAmount, request.Currency ?? "NGN");
        var id = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetSalaryLevelById), new { version = "1.0", id }, new { id });
    }

    /// <summary>
    /// Updates an existing salary level in the organization.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSalaryLevel(
        [FromRoute] Guid id,
        [FromBody] UpdateSalaryLevelApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var command = new UpdateSalaryLevelCommand(id, orgId, request.LevelName, request.BaseAmount, request.Currency ?? "NGN");
        var resultId = await _sender.Send(command, cancellationToken);
        return Ok(new { id = resultId });
    }

    /// <summary>
    /// Deletes a salary level from the organization.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSalaryLevel(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var command = new DeleteSalaryLevelCommand(id, orgId);
        await _sender.Send(command, cancellationToken);
        return NoContent();
    }
}

/// <summary>Request payload for creating a salary level.</summary>
public sealed record CreateSalaryLevelApiRequest(string LevelName, decimal BaseAmount, string? Currency = "NGN");

/// <summary>Request payload for updating a salary level.</summary>
public sealed record UpdateSalaryLevelApiRequest(string LevelName, decimal BaseAmount, string? Currency = "NGN");
