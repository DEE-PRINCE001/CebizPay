using System.Security.Claims;
using CebizPay.Application.Common.Interfaces.Thrift;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// API controller for organization administrators managing workplace-sponsored Thrift (Ajo / Esusu) groups.
/// </summary>
[ApiController]
[Route("api/v1/org/thrift")]
[Authorize]
public sealed class OrgThriftController : ControllerBase
{
    private readonly IThriftGroupService _groupService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrgThriftController"/> class.
    /// </summary>
    public OrgThriftController(IThriftGroupService groupService)
    {
        _groupService = groupService;
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
    /// Creates a new organization workplace Thrift group.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ThriftGroupDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrgGroup(
        [FromBody] CreateThriftGroupRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var userId = GetUserId();

        var scopedRequest = request with { OrganizationId = orgId };
        var group = await _groupService.CreateGroupAsync(userId, scopedRequest, cancellationToken);
        return CreatedAtAction(nameof(GetGroupById), new { id = group.Id }, group);
    }

    /// <summary>
    /// Lists all Thrift groups created within the current organization.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ThriftGroupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrgGroups(CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        var groups = await _groupService.GetGroupsAsync(organizationId: orgId, cancellationToken: cancellationToken);
        return Ok(groups);
    }

    /// <summary>
    /// Returns details of an organization Thrift group.
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
    /// Manually locks positions for an organization Thrift group.
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
}
