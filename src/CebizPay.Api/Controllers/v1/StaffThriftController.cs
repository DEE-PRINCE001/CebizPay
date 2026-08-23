using System.Security.Claims;
using CebizPay.Application.Common.Interfaces.Thrift;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API controller for peer and staff Thrift (Ajo / Esusu) operations: group creation, invitation acceptance, position selection, and cycle tracking.
/// </summary>
[ApiController]
[Route("api/v1/work/thrift")]
[Authorize]
public sealed class StaffThriftController : ControllerBase
{
    private readonly IThriftGroupService _groupService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffThriftController"/> class.
    /// </summary>
    public StaffThriftController(IThriftGroupService groupService)
    {
        _groupService = groupService;
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User ID is missing from token.");
    }

    /// <summary>
    /// Creates a new Thrift group in OpenForMembers status.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ThriftGroupDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateGroup(
        [FromBody] CreateThriftGroupRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var group = await _groupService.CreateGroupAsync(userId, request, cancellationToken);
        return CreatedAtAction(nameof(GetGroupById), new { id = group.Id }, group);
    }

    /// <summary>
    /// Lists thrift groups created by or participated in by the authenticated user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ThriftGroupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroups(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var groups = await _groupService.GetGroupsAsync(userId: userId, cancellationToken: cancellationToken);
        return Ok(groups);
    }

    /// <summary>
    /// Returns the details of a specific thrift group.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ThriftGroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGroupById(Guid id, CancellationToken cancellationToken)
    {
        var group = await _groupService.GetGroupByIdAsync(id, cancellationToken);
        if (group == null)
            return NotFound();

        return Ok(group);
    }

    /// <summary>
    /// Issues an invitation code to invite a member into a thrift group.
    /// </summary>
    [HttpPost("{id:guid}/invite")]
    [ProducesResponseType(typeof(ThriftInvitationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InviteMember(
        Guid id,
        [FromBody] InviteThriftMemberRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var invitation = await _groupService.InviteMemberAsync(id, userId, request, cancellationToken);
        return Ok(invitation);
    }

    /// <summary>
    /// Accepts a thrift invitation code and joins the group.
    /// </summary>
    [HttpPost("join")]
    [ProducesResponseType(typeof(ThriftMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> JoinGroup(
        [FromBody] AcceptThriftInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var member = await _groupService.AcceptInvitationAsync(userId, request, cancellationToken);
        return Ok(member);
    }

    /// <summary>
    /// Selects an available payout rotation position in the thrift group.
    /// </summary>
    [HttpPost("{id:guid}/position")]
    [ProducesResponseType(typeof(ThriftMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SelectPosition(
        Guid id,
        [FromBody] SelectThriftPositionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var member = await _groupService.SelectPositionAsync(id, userId, request, cancellationToken);
        return Ok(member);
    }

    /// <summary>
    /// Lists participating members in the thrift group.
    /// </summary>
    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<ThriftMemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembers(Guid id, CancellationToken cancellationToken)
    {
        var members = await _groupService.GetGroupMembersAsync(id, cancellationToken);
        return Ok(members);
    }

    /// <summary>
    /// Lists scheduled rotation cycles in the thrift group.
    /// </summary>
    [HttpGet("{id:guid}/cycles")]
    [ProducesResponseType(typeof(IReadOnlyList<ThriftCycleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCycles(Guid id, CancellationToken cancellationToken)
    {
        var cycles = await _groupService.GetGroupCyclesAsync(id, cancellationToken);
        return Ok(cycles);
    }

    /// <summary>
    /// Locks payout positions once all members have selected positions.
    /// </summary>
    [HttpPost("{id:guid}/lock")]
    [ProducesResponseType(typeof(ThriftGroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LockPositions(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var group = await _groupService.LockPositionsAsync(id, userId, cancellationToken);
        return Ok(group);
    }

    /// <summary>
    /// Leaves a thrift group and claims net contribution reimbursement.
    /// </summary>
    [HttpPost("{id:guid}/members/{memberId:guid}/leave")]
    [ProducesResponseType(typeof(ThriftReimbursementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LeaveAndReimburse(
        Guid id,
        Guid memberId,
        [FromBody] RemoveThriftMemberRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _groupService.RemoveAndReimburseMemberAsync(id, memberId, userId, request ?? new RemoveThriftMemberRequest(), cancellationToken);
        return Ok(result);
    }
}
