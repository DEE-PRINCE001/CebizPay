using Asp.Versioning;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Admin.Manage;
using CebizPay.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Platform Administrative User Management Controller.
/// Restricts mutating operations strictly to Super Admins, while enabling audit and directory inspection.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/manage")]
public sealed class AdminManageController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminManageController"/>.
    /// </summary>
    public AdminManageController(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <summary>
    /// Retrieves a paginated directory of administrative users.
    /// Accessible to SuperAdmin, Admin, and Auditor roles.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Admin,Auditor")]
    [ProducesResponseType(typeof(PagedResult<AdminProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminDirectory(
        [FromQuery] AdminRoleType? role = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? search = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminDirectoryQuery(role, isActive, search, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Issues a single-use 24-hour invitation for a new administrative user.
    /// Restricted to active Super Admins.
    /// </summary>
    [HttpPost("invite")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(InviteAdminResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InviteAdmin(
        [FromBody] InviteAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new InviteAdminCommand(request.Email, request.Role);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Toggles the active/inactive state of an administrative user profile.
    /// Restricted to active Super Admins.
    /// </summary>
    [HttpPatch("toggle-status")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(AdminProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleAdminStatus(
        [FromBody] ToggleAdminStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new ToggleAdminStatusCommand(request.AdminProfileId, request.IsActive);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Soft deletes / archives an administrative user profile.
    /// Restricted to active Super Admins.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAdmin(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteAdminCommand(id);
        await _sender.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Redeems an administrative invitation token and completes onboarding credentials.
    /// Unauthenticated endpoint for newly invited administrators.
    /// </summary>
    [HttpPost("redeem-invite")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RedeemAdminInviteResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RedeemAdminInvite(
        [FromBody] RedeemAdminInviteRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new RedeemAdminInviteCommand(request.InvitationToken, request.Password, request.PhoneNumber);
        var result = await _sender.Send(command, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
