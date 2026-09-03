using Asp.Versioning;
using CebizPay.Application.UseCases.Referrals;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Platform referral program administration endpoints.
/// Super Admin may view and update global parameters. Auditor may view configuration read-only.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/referrals")]
public sealed class AdminReferralsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminReferralsController"/>.
    /// </summary>
    public AdminReferralsController(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <summary>
    /// Retrieves the active global referral configuration parameters.
    /// Super Admin and Auditor authorized.
    /// </summary>
    [HttpGet("settings")]
    [Authorize(Roles = "SuperAdmin,Auditor")]
    [ProducesResponseType(typeof(ReferralSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetReferralSettingQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates the active global referral configuration parameters.
    /// Super Admin only. Modification is audit-logged.
    /// </summary>
    [HttpPut("settings")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ReferralSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] UpdateReferralSettingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new UpdateReferralSettingCommand(
            request.RewardAmountPerSuccessfulReferral,
            request.MaximumSuccessfulReferralsPerUser,
            request.IsActive), cancellationToken);

        return Ok(result);
    }
}
