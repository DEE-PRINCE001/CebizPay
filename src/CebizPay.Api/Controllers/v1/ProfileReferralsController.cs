using Asp.Versioning;
using CebizPay.Application.UseCases.Referrals;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Authenticated user referral program endpoints.
/// Allows users to view their referral dashboard, generate referral codes, and claim invitations.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/profile/referrals")]
[Authorize]
public sealed class ProfileReferralsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="ProfileReferralsController"/>.
    /// </summary>
    public ProfileReferralsController(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <summary>
    /// Retrieves the authenticated user's referral program dashboard and referral history.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ReferralDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetReferralDashboardQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves or generates a unique, collision-resistant referral code for the authenticated user.
    /// </summary>
    [HttpPost("code")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetOrCreateCode(CancellationToken cancellationToken)
    {
        var code = await _sender.Send(new GetOrCreateReferralCodeCommand(), cancellationToken);
        return Ok(new { referralCode = code });
    }

    /// <summary>
    /// Claims a referral code to associate the authenticated user with a referring user.
    /// Strictly rejects self-referral and duplicate claims.
    /// </summary>
    [HttpPost("claim")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ClaimCode(
        [FromBody] ClaimReferralCodeRequest request,
        CancellationToken cancellationToken)
    {
        var relationshipId = await _sender.Send(new ClaimReferralCodeCommand(request.ReferralCode), cancellationToken);
        return Ok(new { relationshipId });
    }
}
