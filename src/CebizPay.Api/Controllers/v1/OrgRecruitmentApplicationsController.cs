using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Organizations.Recruitment;
using CebizPay.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API endpoints for organization recruiters/HR managers to review candidate applications.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/recruitment")]
[Authorize]
public sealed class OrgRecruitmentApplicationsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="OrgRecruitmentApplicationsController"/>.
    /// </summary>
    public OrgRecruitmentApplicationsController(ISender sender, ICurrentOrganizationContext orgContext)
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
    /// Lists all candidate applications submitted for a specific job posting.
    /// </summary>
    [HttpGet("jobs/{jobId:guid}/applications")]
    [ProducesResponseType(typeof(PagedResult<RecruitmentApplicationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJobApplications(
        [FromRoute] Guid jobId,
        [FromQuery] ApplicationStatus? status,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetJobApplicationsQuery(jobId, orgId, status, search, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets detailed profile and review history of a single application.
    /// </summary>
    [HttpGet("applications/{id:guid}")]
    [ProducesResponseType(typeof(RecruitmentApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApplicationById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetApplicationByIdQuery(id, orgId);
        var result = await _sender.Send(query, cancellationToken);
        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Application Not Found",
                Detail = $"Application '{id}' was not found in this organization."
            });
        }
        return Ok(result);
    }

    /// <summary>
    /// Moves a candidate application to under review status.
    /// </summary>
    [HttpPost("applications/{id:guid}/review")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewApplication(
        [FromRoute] Guid id,
        [FromBody] ReviewApplicationApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var command = new ReviewApplicationCommand(id, orgId, request.Notes);
        await _sender.Send(command, cancellationToken);
        return Ok(new { message = "Application moved to under review status." });
    }

    /// <summary>
    /// Shortlists a candidate application.
    /// </summary>
    [HttpPost("applications/{id:guid}/shortlist")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ShortlistApplication(
        [FromRoute] Guid id,
        [FromBody] ShortlistApplicationApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var command = new ShortlistApplicationCommand(id, orgId, request.Notes);
        await _sender.Send(command, cancellationToken);
        return Ok(new { message = "Candidate shortlisted successfully." });
    }

    /// <summary>
    /// Rejects a candidate application with feedback/reason.
    /// </summary>
    [HttpPost("applications/{id:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectApplication(
        [FromRoute] Guid id,
        [FromBody] RejectApplicationApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var command = new RejectApplicationCommand(id, orgId, request.RejectionReason, request.Notes);
        await _sender.Send(command, cancellationToken);
        return Ok(new { message = "Application rejected." });
    }

    /// <summary>
    /// Accepts a candidate application (extends job offer).
    /// </summary>
    [HttpPost("applications/{id:guid}/accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptApplication(
        [FromRoute] Guid id,
        [FromBody] AcceptApplicationApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var command = new AcceptApplicationCommand(id, orgId, request.Notes);
        await _sender.Send(command, cancellationToken);
        return Ok(new { message = "Candidate application accepted successfully." });
    }
}
