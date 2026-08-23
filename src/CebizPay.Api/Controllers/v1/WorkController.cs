using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.StaffInvitations.AcceptInvitation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Mobile Work domain endpoints for individual staff and workers.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/work")]
[Authorize]
public sealed class WorkController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="WorkController"/>.
    /// </summary>
    public WorkController(ISender sender, ICurrentUserService currentUserService)
    {
        _sender = sender;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Individual joins an organization by submitting an invitation code from the mobile Work domain.
    /// </summary>
    [HttpPost("organisation/join")]
    [ProducesResponseType(typeof(AcceptStaffInvitationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> JoinOrganization(
        [FromBody] JoinOrganizationApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "User context is missing from token."
            });
        }

        var command = new AcceptStaffInvitationCommand(request.InvitationCode, userId);
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }
}

/// <summary>Request payload for joining an organization with an invitation code.</summary>
public sealed record JoinOrganizationApiRequest(string InvitationCode);
