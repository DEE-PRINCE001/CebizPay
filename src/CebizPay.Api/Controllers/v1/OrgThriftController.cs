using System.Security.Claims;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Interfaces.Thrift;
using CebizPay.Domain.Permissions;
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
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrgThriftController"/> class.
    /// </summary>
    public OrgThriftController(
        IThriftGroupService groupService,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService)
    {
        _groupService = groupService;
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
    /// Creates a new organization workplace Thrift group.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ThriftGroupDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateOrgGroup(
        [FromBody] CreateThriftGroupRequest request,
        CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.ThriftCreate, cancellationToken))
        {
            return Forbid();
        }

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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrgGroups(CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.ThriftView, cancellationToken))
        {
            return Forbid();
        }

        var groups = await _groupService.GetGroupsAsync(organizationId: orgId, cancellationToken: cancellationToken);
        return Ok(groups);
    }

    /// <summary>
    /// Returns details of an organization Thrift group.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ThriftGroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetGroupById(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.ThriftView, cancellationToken))
        {
            return Forbid();
        }

        var group = await _groupService.GetGroupByIdAsync(id, cancellationToken);
        if (group == null)
            return NotFound();

        if (group.OrganizationId != orgId)
            return Forbid();

        return Ok(group);
    }

    /// <summary>
    /// Manually locks positions for an organization Thrift group.
    /// </summary>
    [HttpPost("{id:guid}/lock")]
    [ProducesResponseType(typeof(ThriftGroupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> LockPositions(Guid id, CancellationToken cancellationToken)
    {
        var orgId = GetOrganizationId();
        if (!await _orgContext.HasPermissionAsync(orgId, Permissions.ThriftManage, cancellationToken))
        {
            return Forbid();
        }

        var userId = GetUserId();
        var group = await _groupService.LockPositionsAsync(id, userId, cancellationToken);
        return Ok(group);
    }
}
