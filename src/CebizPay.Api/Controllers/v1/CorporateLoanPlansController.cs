using System.Security.Claims;
using CebizPay.Application.Common.Interfaces.Loans;
using CebizPay.Domain.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API controller for managing organization corporate loan plans.
/// </summary>
[ApiController]
[Route("api/v1/org/loan-plans")]
[Authorize]
public sealed class CorporateLoanPlansController : ControllerBase
{
    private readonly ILoanPlanService _planService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorporateLoanPlansController"/> class.
    /// </summary>
    public CorporateLoanPlansController(ILoanPlanService planService)
    {
        _planService = planService;
    }

    private Guid GetOrganizationId()
    {
        var orgIdClaim = User.FindFirstValue("OrganizationId") ?? User.FindFirstValue("org_id");
        if (string.IsNullOrEmpty(orgIdClaim) || !Guid.TryParse(orgIdClaim, out var orgId))
        {
            throw new UnauthorizedAccessException("Organization context is missing from token.");
        }
        return orgId;
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User ID is missing from token.");
    }

    /// <summary>
    /// Creates a new corporate loan plan for the organization.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CorporateLoanPlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePlan(
        [FromBody] CreateLoanPlanRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var userId = GetUserId();

        var plan = await _planService.CreatePlanAsync(orgId, request, userId, cancellationToken);
        return CreatedAtAction(nameof(GetPlanById), new { id = plan.Id }, plan);
    }

    /// <summary>
    /// Lists all corporate loan plans for the organization.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CorporateLoanPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlans(
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        var plans = await _planService.GetPlansForOrgAsync(orgId, activeOnly, cancellationToken);
        return Ok(plans);
    }

    /// <summary>
    /// Gets a single corporate loan plan by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CorporateLoanPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlanById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var plan = await _planService.GetPlanByIdAsync(orgId, id, cancellationToken);
        if (plan == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Plan Not Found",
                Detail = $"Corporate loan plan '{id}' was not found."
            });
        }
        return Ok(plan);
    }

    /// <summary>
    /// Updates an existing corporate loan plan.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CorporateLoanPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePlan(
        Guid id,
        [FromBody] UpdateLoanPlanRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var userId = GetUserId();

        var updated = await _planService.UpdatePlanAsync(orgId, id, request, userId, cancellationToken);
        return Ok(updated);
    }
}
