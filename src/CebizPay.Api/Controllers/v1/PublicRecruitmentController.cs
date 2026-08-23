using Asp.Versioning;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Organizations.Recruitment;
using CebizPay.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Public and candidate-facing API endpoints for browsing jobs, submitting applications, and withdrawing applications.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/recruitment")]
public sealed class PublicRecruitmentController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="PublicRecruitmentController"/>.
    /// </summary>
    public PublicRecruitmentController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Publicly browses active published job openings with optional filters and search.
    /// </summary>
    [HttpGet("jobs")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<PublicJobPostingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublicJobs(
        [FromQuery] string? search,
        [FromQuery] string? location,
        [FromQuery] EmploymentType? employmentType,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPublicJobPostingsQuery(search, location, employmentType, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Publicly gets details for an active published job opening.
    /// </summary>
    [HttpGet("jobs/{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PublicJobPostingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicJobById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPublicJobPostingByIdQuery(id);
        var result = await _sender.Send(query, cancellationToken);
        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Job Opening Not Found",
                Detail = $"Job opening '{id}' is either not found, not published, or has expired."
            });
        }
        return Ok(result);
    }

    /// <summary>
    /// Submits a candidate job application for an active job opening.
    /// </summary>
    [HttpPost("jobs/{jobId:guid}/applications")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitApplication(
        [FromRoute] Guid jobId,
        [FromBody] SubmitApplicationApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new SubmitApplicationCommand(
            jobId,
            request.ApplicantName,
            request.ApplicantEmail,
            request.ApplicantPhone,
            request.ResumeReference,
            request.CoverLetter);

        var applicationId = await _sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new { id = applicationId, message = "Application submitted successfully." });
    }

    /// <summary>
    /// Allows candidate to withdraw their active job application.
    /// </summary>
    [HttpPost("applications/{id:guid}/withdraw")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> WithdrawApplication(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var command = new WithdrawApplicationCommand(id);
        await _sender.Send(command, cancellationToken);
        return Ok(new { message = "Application withdrawn successfully." });
    }
}
