using CebizPay.Application.Common.Interfaces.Savings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API controller for Super Admin configuration of platform savings interest policies and effective rates.
/// </summary>
[ApiController]
[Route("api/v1/admin/savings/interest-policies")]
[Authorize(Policy = CebizPay.Application.Common.Security.AuthorizationPolicies.RequirePlatformAdmin)]
public sealed class AdminSavingsInterestPoliciesController : ControllerBase
{
    private readonly ISavingsInterestPolicyService _policyService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminSavingsInterestPoliciesController"/> class.
    /// </summary>
    public AdminSavingsInterestPoliciesController(ISavingsInterestPolicyService policyService)
    {
        _policyService = policyService;
    }

    /// <summary>
    /// Lists all historical and active savings interest policies.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SavingsInterestPolicyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPolicies(CancellationToken cancellationToken)
    {
        var policies = await _policyService.GetAllPoliciesAsync(cancellationToken);
        return Ok(policies);
    }

    /// <summary>
    /// Creates and activates a new interest policy version, atomically superseding prior versions.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = CebizPay.Application.Common.Security.AuthorizationPolicies.RequireSuperAdmin)]
    [ProducesResponseType(typeof(SavingsInterestPolicyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePolicy(
        [FromBody] CreateSavingsInterestPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var policy = await _policyService.CreateAndActivatePolicyAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetPolicies), new { id = policy.Id }, policy);
    }
}
