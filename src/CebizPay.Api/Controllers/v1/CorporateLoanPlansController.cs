using System.Security.Claims;
using CebizPay.Application.Common.Interfaces.Loans;
using CebizPay.Application.Common.Interfaces.Security;
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
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorporateLoanPlansController"/> class.
    /// </summary>
    public CorporateLoanPlansController(
        ILoanPlanService planService,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService)
    {
        _planService = planService;
        _orgContext = orgContext;
        _currentUserService = currentUserService;
    }

    private Guid GetOrganizationId()
    {
        return _orgContext.CurrentOrganizationId
            ?? throw new UnauthorizedAccessException("Organization context is missing from request. Provide a valid 'X-Organization-Id' header.");
    }

    private string GetUserId()
    {
        return _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User authentication context is required.");
    }

    /// <summary>
    /// Creates a new corporate loan plan for the organization.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CorporateLoanPlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePlan(
        [FromBody] CreateLoanPlanRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.LoanManagePlan, cancellationToken))
        {
            return Forbid();
        }

        var userId = GetUserId();

        var plan = await _planService.CreatePlanAsync(orgId, request, userId, cancellationToken);
        return CreatedAtAction(nameof(GetPlanById), new { id = plan.Id }, plan);
    }

    /// <summary>
    /// Lists all corporate loan plans for the organization.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CorporateLoanPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPlans(
        [FromQuery] bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.LoanView, cancellationToken))
        {
            return Forbid();
        }

        var plans = await _planService.GetPlansForOrgAsync(orgId, activeOnly, cancellationToken);
        return Ok(plans);
    }

    /// <summary>
    /// Gets a single corporate loan plan by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CorporateLoanPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPlanById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.LoanView, cancellationToken))
        {
            return Forbid();
        }

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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdatePlan(
        Guid id,
        [FromBody] UpdateLoanPlanRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.LoanManagePlan, cancellationToken))
        {
            return Forbid();
        }

        var userId = GetUserId();

        var updated = await _planService.UpdatePlanAsync(orgId, id, request, userId, cancellationToken);
        return Ok(updated);
    }
}
