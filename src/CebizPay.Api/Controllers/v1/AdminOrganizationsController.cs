using Asp.Versioning;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Admin.Organizations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Platform Administrative Organizations Directory and Management Controller.
/// Enables platform administrators (SuperAdmin, Admin, Auditor) to inspect organizations, staff rosters, and compliance credentials.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/organizations")]
[Authorize(Roles = "SuperAdmin,Admin,Auditor")]
public sealed class AdminOrganizationsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminOrganizationsController"/>.
    /// </summary>
    public AdminOrganizationsController(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <summary>
    /// Retrieves a paginated directory of all onboarded organizations across the platform.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AdminOrganizationSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrganizationsDirectory(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? category = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminOrganizationsDirectoryQuery(pageNumber, pageSize, search, status, category);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Exports filtered organizations dataset directly as a downloadable CSV file.
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportOrganizations(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        var query = new ExportAdminOrganizationsQuery(search, status, format);
        var result = await _sender.Send(query, cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Retrieves full operational and KYB profile details for a specific organization by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AdminOrganizationDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrganizationById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminOrganizationDetailsQuery(id);
        var result = await _sender.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Organization Not Found",
                Detail = $"Organization with ID '{id}' was not found."
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Retrieves staff members associated with a specific organization in platform admin scope.
    /// </summary>
    [HttpGet("{id:guid}/staff")]
    [ProducesResponseType(typeof(PagedResult<AdminStaffRosterItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrganizationStaff(
        [FromRoute] Guid id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetAdminOrganizationStaffRosterQuery(id, pageNumber, pageSize, search);
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Organization Not Found",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Retrieves verification document files submitted during KYB onboarding for a specific organization.
    /// </summary>
    [HttpGet("{id:guid}/documents")]
    [ProducesResponseType(typeof(AdminOrganizationDocumentsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrganizationDocuments(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetAdminOrganizationDocumentsQuery(id);
            var result = await _sender.Send(query, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Organization Not Found",
                Detail = ex.Message
            });
        }
    }
}
