using System.Security.Claims;
using CebizPay.Application.Common.Interfaces.Savings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API controller for organization administrators managing corporate-sponsored savings schemes.
/// </summary>
[ApiController]
[Route("api/v1/org/savings")]
[Authorize]
public sealed class OrgSavingsController : ControllerBase
{
    private readonly ISavingsService _savingsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrgSavingsController"/> class.
    /// </summary>
    public OrgSavingsController(ISavingsService savingsService)
    {
        _savingsService = savingsService;
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
    /// Creates a new organization-sponsored savings plan.
    /// </summary>
    [HttpPost("plans")]
    [ProducesResponseType(typeof(SavingsPlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePlan(
        [FromBody] CreateSavingsPlanRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var userId = GetUserId();

        var scopedRequest = request with { OrganizationId = orgId, OwnerType = Domain.Savings.Enums.SavingsOwnerType.Organization };
        var plan = await _savingsService.CreatePlanAsync(userId, scopedRequest, cancellationToken);
        return CreatedAtAction(nameof(GetPlanById), new { id = plan.Id }, plan);
    }

    /// <summary>
    /// Lists all savings plans sponsored by the current organization.
    /// </summary>
    [HttpGet("plans")]
    [ProducesResponseType(typeof(IReadOnlyList<SavingsPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var plans = await _savingsService.GetAvailablePlansAsync(orgId, cancellationToken);
        return Ok(plans);
    }

    /// <summary>
    /// Returns details of an organization savings plan.
    /// </summary>
    [HttpGet("plans/{id:guid}")]
    [ProducesResponseType(typeof(SavingsPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlanById(Guid id, CancellationToken cancellationToken)
    {
        var plan = await _savingsService.GetPlanByIdAsync(id, cancellationToken);
        if (plan == null)
            return NotFound();

        return Ok(plan);
    }

    /// <summary>
    /// Lists participant savings accounts enrolled in the organization's sponsored plan.
    /// </summary>
    [HttpGet("plans/{id:guid}/participants")]
    [ProducesResponseType(typeof(IReadOnlyList<SavingsAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetParticipants(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var accounts = await _savingsService.GetAccountsAsync(organizationId: orgId, cancellationToken: cancellationToken);
        var planAccounts = accounts.Where(a => a.SavingsPlanId == id).ToList();
        return Ok(planAccounts);
    }
}
