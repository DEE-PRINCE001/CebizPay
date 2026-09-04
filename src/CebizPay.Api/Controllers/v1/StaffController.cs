using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Organizations.Staff;
using CebizPay.Application.UseCases.StaffInvitations.AcceptInvitation;
using CebizPay.Application.UseCases.StaffInvitations.InviteStaff;
using CebizPay.Application.UseCases.StaffInvitations.SuspendStaff;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API endpoints for managing organization staff members, invitations, workforce assignments, and lifecycle.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/org/staff")]
[Authorize]
public sealed class StaffController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="StaffController"/>.
    /// </summary>
    public StaffController(ISender sender, ICurrentOrganizationContext orgContext)
    {
        _sender = sender;
        _orgContext = orgContext;
    }

    private Guid GetOrganizationId()
    {
        var orgId = _orgContext.CurrentOrganizationId;
        if (!orgId.HasValue || orgId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Active organization context is required.");
        }
        return orgId.Value;
    }

    /// <summary>
    /// Lists all staff members for the organization with filtering, pagination, and search.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<StaffSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStaffDirectory(
        [FromQuery] string? search,
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? roleId,
        [FromQuery] Guid? salaryLevelId,
        [FromQuery] MembershipStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.StaffView, cancellationToken))
        {
            return Forbid();
        }

        var query = new GetStaffDirectoryQuery(orgId, search, departmentId, roleId, salaryLevelId, status, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets detailed profile for a specific staff membership.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StaffProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStaffProfile(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.StaffView, cancellationToken))
        {
            return Forbid();
        }

        var query = new GetStaffProfileQuery(orgId, id);
        var result = await _sender.Send(query, cancellationToken);
        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Staff Member Not Found",
                Detail = $"Staff membership '{id}' was not found in this organization."
            });
        }
        return Ok(result);
    }

    /// <summary>
    /// Directly onboards/creates a staff member in the organization without an invitation.
    /// </summary>
    [HttpPost("create")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateStaffDirect(
        [FromBody] CreateStaffDirectApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.StaffCreate, cancellationToken) &&
            !await _orgContext.HasPermissionAsync(orgId, Permissions.StaffManage, cancellationToken))
        {
            return Forbid();
        }

        var command = new CreateStaffDirectCommand(
            orgId,
            request.Email,
            request.FirstName,
            request.LastName,
            request.PhoneNumber,
            request.DepartmentId,
            request.WorkforceRoleId,
            request.SalaryLevelId,
            request.Role ?? MembershipRoleType.Member);
        var membershipId = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetStaffProfile), new { version = "1.0", id = membershipId }, new { id = membershipId });
    }

    /// <summary>
    /// Organization invites a single staff member via email.
    /// </summary>
    [HttpPost("invite")]
    [ProducesResponseType(typeof(InviteStaffResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InviteStaff(
        [FromBody] InviteStaffApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.StaffInvite, cancellationToken) &&
            !await _orgContext.HasPermissionAsync(orgId, Permissions.StaffManage, cancellationToken))
        {
            return Forbid();
        }

        var command = new InviteStaffCommand(orgId, request.Email);
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Organization sends bulk staff invitations.
    /// </summary>
    [HttpPost("invite-bulk")]
    [ProducesResponseType(typeof(BulkInviteSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InviteStaffBulk(
        [FromBody] InviteStaffBulkApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.StaffInvite, cancellationToken) &&
            !await _orgContext.HasPermissionAsync(orgId, Permissions.StaffManage, cancellationToken))
        {
            return Forbid();
        }

        var command = new InviteStaffBulkCommand(orgId, request.Emails);
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Individual accepts a staff invitation.
    /// </summary>
    [HttpPost("accept")]
    [ProducesResponseType(typeof(AcceptStaffInvitationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptInvitation(
        [FromBody] AcceptStaffInvitationCommand command,
        CancellationToken cancellationToken = default)
    {
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Assigns or reassigns workforce details (Department, Role, Salary Level) to a staff member.
    /// </summary>
    [HttpPut("{id:guid}/assign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AssignStaffWorkforce(
        [FromRoute] Guid id,
        [FromBody] AssignStaffWorkforceApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.StaffAssign, cancellationToken) &&
            !await _orgContext.HasPermissionAsync(orgId, Permissions.StaffManage, cancellationToken))
        {
            return Forbid();
        }

        var command = new AssignStaffWorkforceCommand(orgId, id, request.DepartmentId, request.WorkforceRoleId, request.SalaryLevelId);
        await _sender.Send(command, cancellationToken);
        return Ok(new { success = true, message = "Staff workforce details assigned successfully." });
    }

    /// <summary>
    /// Organization suspends a staff member's work relationship.
    /// </summary>
    [HttpPatch("{id:guid}/suspend")]
    [ProducesResponseType(typeof(SuspendStaffMembershipResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SuspendStaff(
        [FromRoute] Guid id,
        [FromBody] SuspendStaffApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.StaffManage, cancellationToken))
        {
            return Forbid();
        }

        var command = new SuspendStaffMembershipCommand(id, request.Reason);
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Organization reactivates a suspended staff member's work relationship.
    /// </summary>
    [HttpPatch("{id:guid}/reactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReactivateStaff(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.StaffReactivate, cancellationToken) &&
            !await _orgContext.HasPermissionAsync(orgId, Permissions.StaffManage, cancellationToken))
        {
            return Forbid();
        }

        var command = new ReactivateStaffMembershipCommand(orgId, id);
        await _sender.Send(command, cancellationToken);
        return Ok(new { success = true, message = "Staff membership reactivated successfully." });
    }

    /// <summary>
    /// Organization terminates a staff member's work relationship and converts corporate payroll loans.
    /// </summary>
    [HttpPost("{id:guid}/terminate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TerminateStaff(
        [FromRoute] Guid id,
        [FromBody] TerminateStaffApiRequest request,
        CancellationToken cancellationToken = default)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.StaffTerminate, cancellationToken) &&
            !await _orgContext.HasPermissionAsync(orgId, Permissions.StaffManage, cancellationToken))
        {
            return Forbid();
        }
        var command = new TerminateStaffMembershipCommand(orgId, id, request.Reason);
        await _sender.Send(command, cancellationToken);
        return Ok(new { success = true, message = "Staff membership terminated and corporate loans converted successfully." });
    }
}

/// <summary>Request payload for direct staff creation.</summary>
public sealed record CreateStaffDirectApiRequest(
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber = null,
    Guid? DepartmentId = null,
    Guid? WorkforceRoleId = null,
    Guid? SalaryLevelId = null,
    MembershipRoleType? Role = null);

/// <summary>Request payload for single staff invitation.</summary>
public sealed record InviteStaffApiRequest(string Email);

/// <summary>Request payload for bulk staff invitations.</summary>
public sealed record InviteStaffBulkApiRequest(List<string> Emails);

/// <summary>Request payload for staff workforce assignment.</summary>
public sealed record AssignStaffWorkforceApiRequest(Guid? DepartmentId, Guid? WorkforceRoleId, Guid? SalaryLevelId);

/// <summary>Request payload for staff suspension.</summary>
public sealed record SuspendStaffApiRequest(string Reason);

/// <summary>Request payload for staff termination.</summary>
public sealed record TerminateStaffApiRequest(string Reason);
