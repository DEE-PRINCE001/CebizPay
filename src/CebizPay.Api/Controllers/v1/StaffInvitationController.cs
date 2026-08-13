using Asp.Versioning;
using CebizPay.Application.UseCases.StaffInvitations.AcceptInvitation;
using CebizPay.Application.UseCases.StaffInvitations.InviteStaff;
using CebizPay.Application.UseCases.StaffInvitations.SuspendStaff;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Staff Invitation and Membership endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/staff")]
public sealed class StaffInvitationController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="StaffInvitationController"/>.
    /// </summary>
    public StaffInvitationController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Organization invites a staff member.
    /// </summary>
    [HttpPost("invite")]
    [Authorize]
    public async Task<IActionResult> InviteStaff([FromBody] InviteStaffCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Individual accepts a staff invitation.
    /// </summary>
    [HttpPost("accept")]
    [Authorize]
    public async Task<IActionResult> AcceptInvitation([FromBody] AcceptStaffInvitationCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Organization suspends staff member's work relationship.
    /// </summary>
    [HttpPatch("{membershipId:guid}/suspend")]
    [Authorize]
    public async Task<IActionResult> SuspendStaff(
        [FromRoute] Guid membershipId,
        [FromBody] SuspendStaffRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SuspendStaffMembershipCommand(membershipId, request.Reason);
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }
}

/// <summary>Request payload for staff suspension.</summary>
public sealed record SuspendStaffRequest(string Reason);
