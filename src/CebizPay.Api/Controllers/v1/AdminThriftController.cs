using Asp.Versioning;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Admin.ThriftOversight;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Thrift.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Platform Administrative Thrift Oversight Controller.
/// Provides platform-level monitoring of rotational savings (Ajo / Esusu) groups, delinquency intervention, and dispute resolution.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/thrifts")]
public sealed class AdminThriftController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminThriftController"/>.
    /// </summary>
    public AdminThriftController(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <summary>
    /// Retrieves a paginated directory of all platform Thrift groups.
    /// Accessible to SuperAdmin, Admin, and Auditor roles.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Admin,Auditor")]
    [ProducesResponseType(typeof(PagedResult<AdminThriftGroupSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetThriftDirectory(
        [FromQuery] ThriftStatus? status = null,
        [FromQuery] ThriftFrequency? frequency = null,
        [FromQuery] Currency? currency = null,
        [FromQuery] Guid? organizationId = null,
        [FromQuery] string? search = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminThriftDirectoryQuery(status, frequency, currency, organizationId, search, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves full oversight details for a specific Thrift group, including members, cycles, and dispute history.
    /// Accessible to SuperAdmin, Admin, and Auditor roles.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Admin,Auditor")]
    [ProducesResponseType(typeof(AdminThriftGroupDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetThriftGroupDetails(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminThriftGroupDetailsQuery(id);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves the platform-wide Thrift delinquency and overdue member oversight queue.
    /// Accessible to SuperAdmin, Admin, and Auditor roles.
    /// </summary>
    [HttpGet("delinquencies")]
    [Authorize(Roles = "SuperAdmin,Admin,Auditor")]
    [ProducesResponseType(typeof(PagedResult<AdminThriftDelinquencyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetThriftDelinquencies(
        [FromQuery] string? search = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminThriftDelinquenciesQuery(search, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Pauses an active or locked Thrift group for investigation or administrative intervention.
    /// Restricted to Super Admins.
    /// </summary>
    [HttpPost("{id:guid}/pause")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PauseThriftGroup(
        Guid id,
        [FromBody] PauseThriftGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new PauseThriftGroupCommand(id, request.Reason);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(new { Succeeded = result, GroupId = id, Status = "Paused" });
    }

    /// <summary>
    /// Resumes a previously paused Thrift group.
    /// Restricted to Super Admins.
    /// </summary>
    [HttpPost("{id:guid}/resume")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResumeThriftGroup(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var command = new ResumeThriftGroupCommand(id);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(new { Succeeded = result, GroupId = id, Status = "Resumed" });
    }

    /// <summary>
    /// Retrieves a paginated list of Thrift oversight disputes.
    /// Accessible to SuperAdmin, Admin, and Auditor roles.
    /// </summary>
    [HttpGet("disputes")]
    [Authorize(Roles = "SuperAdmin,Admin,Auditor")]
    [ProducesResponseType(typeof(PagedResult<ThriftDisputeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetThriftDisputes(
        [FromQuery] ThriftDisputeStatus? status = null,
        [FromQuery] Guid? thriftGroupId = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminThriftDisputesQuery(status, thriftGroupId, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lodges a new Thrift dispute or oversight issue.
    /// Accessible to authenticated platform users and admins.
    /// </summary>
    [HttpPost("disputes")]
    [Authorize]
    [ProducesResponseType(typeof(ThriftDisputeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateThriftDispute(
        [FromBody] CreateThriftDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateThriftDisputeCommand(request.ThriftGroupId, request.CycleId, request.MemberId, request.Reason);
        var result = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetThriftDisputes), new { id = result.Id }, result);
    }

    /// <summary>
    /// Resolves or dismisses a Thrift dispute with administrative findings.
    /// Restricted to Super Admins.
    /// </summary>
    [HttpPost("disputes/{id:guid}/resolve")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(ThriftDisputeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveThriftDispute(
        Guid id,
        [FromBody] ResolveThriftDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new ResolveThriftDisputeCommand(id, request.ResolutionNotes, request.Reject);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }
}
