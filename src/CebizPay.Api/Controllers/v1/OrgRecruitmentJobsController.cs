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
/// API endpoints for organization recruiters/managers to manage job postings.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/recruitment/jobs")]
[Authorize]
public sealed class OrgRecruitmentJobsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="OrgRecruitmentJobsController"/>.
    /// </summary>
    public OrgRecruitmentJobsController(ISender sender, ICurrentOrganizationContext orgContext)
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
    /// Lists all job postings for the active organization with optional filters, search, and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<JobPostingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJobPostings(
        [FromQuery] JobPostingStatus? status,
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? roleId,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetOrgJobPostingsQuery(orgId, status, departmentId, roleId, search, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets a single job posting by ID with full details and applicant count.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JobPostingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJobPostingById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var query = new GetOrgJobPostingByIdQuery(id, orgId);
        var result = await _sender.Send(query, cancellationToken);
        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Job Posting Not Found",
                Detail = $"Job posting '{id}' was not found in this organization."
            });
        }
        return Ok(result);
    }

    /// <summary>
    /// Creates a new draft job posting.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateJobPosting(
        [FromBody] CreateJobPostingApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var command = new CreateJobPostingCommand(
            orgId,
            request.Title,
            request.Description,
            request.EmploymentType,
            request.DepartmentId,
            request.WorkforceRoleId,
            request.SalaryLevelId,
            request.Location,
            request.Requirements,
            request.Responsibilities,
            request.ApplicationDeadline);

        var id = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetJobPostingById), new { version = "1.0", id }, new { id });
    }

    /// <summary>
    /// Updates details of an existing job posting.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateJobPosting(
        [FromRoute] Guid id,
        [FromBody] UpdateJobPostingApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var command = new UpdateJobPostingCommand(
            id,
            orgId,
            request.Title,
            request.Description,
            request.EmploymentType,
            request.DepartmentId,
            request.WorkforceRoleId,
            request.SalaryLevelId,
            request.Location,
            request.Requirements,
            request.Responsibilities,
            request.ApplicationDeadline);

        var resultId = await _sender.Send(command, cancellationToken);
        return Ok(new { id = resultId });
    }

    /// <summary>
    /// Publishes a draft job posting to start receiving candidate applications.
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishJobPosting(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var command = new PublishJobPostingCommand(id, orgId);
        await _sender.Send(command, cancellationToken);
        return Ok(new { message = "Job posting published successfully." });
    }

    /// <summary>
    /// Closes an active job posting, terminating candidate application intake.
    /// </summary>
    [HttpPost("{id:guid}/close")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CloseJobPosting(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var command = new CloseJobPostingCommand(id, orgId);
        await _sender.Send(command, cancellationToken);
        return Ok(new { message = "Job posting closed successfully." });
    }

    /// <summary>
    /// Cancels a draft or published job posting.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelJobPosting(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var command = new CancelJobPostingCommand(id, orgId);
        await _sender.Send(command, cancellationToken);
        return Ok(new { message = "Job posting cancelled successfully." });
    }
}
