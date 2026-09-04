using System.Security.Claims;
using CebizPay.Application.Common.Interfaces.Savings;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Permissions;
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
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrgSavingsController"/> class.
    /// </summary>
    public OrgSavingsController(
        ISavingsService savingsService,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService)
    {
        _savingsService = savingsService;
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
            ?? throw new UnauthorizedAccessException("User ID is missing from token.");
    }

    /// <summary>
    /// Creates a new organization-sponsored savings plan.
    /// </summary>
    [HttpPost("plans")]
    [ProducesResponseType(typeof(SavingsPlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePlan(
        [FromBody] CreateSavingsPlanRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.SavingsManagePlan, cancellationToken))
        {
            return Forbid();
        }

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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.SavingsView, cancellationToken))
        {
            return Forbid();
        }

        var plans = await _savingsService.GetAvailablePlansAsync(orgId, cancellationToken);
        return Ok(plans);
    }

    /// <summary>
    /// Returns details of an organization savings plan.
    /// </summary>
    [HttpGet("plans/{id:guid}")]
    [ProducesResponseType(typeof(SavingsPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPlanById(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.SavingsView, cancellationToken))
        {
            return Forbid();
        }

        var plan = await _savingsService.GetPlanByIdAsync(id, cancellationToken);
        if (plan == null)
            return NotFound();

        if (plan.OrganizationId != orgId)
            return Forbid();

        return Ok(plan);
    }

    /// <summary>
    /// Lists participant savings accounts enrolled in the organization's sponsored plan.
    /// </summary>
    [HttpGet("plans/{id:guid}/participants")]
    [ProducesResponseType(typeof(IReadOnlyList<SavingsAccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetParticipants(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.SavingsView, cancellationToken))
        {
            return Forbid();
        }

        var accounts = await _savingsService.GetAccountsAsync(organizationId: orgId, cancellationToken: cancellationToken);
        var planAccounts = accounts.Where(a => a.SavingsPlanId == id).ToList();
        return Ok(planAccounts);
    }
}
