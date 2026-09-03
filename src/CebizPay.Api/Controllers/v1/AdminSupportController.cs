using Asp.Versioning;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Support;
using CebizPay.Domain.Support.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Administrative governance endpoints for support ticket oversight, resolution, operator messaging, and reports.
/// Restricted to SuperAdmin and Auditor.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/support")]
public sealed class AdminSupportController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminSupportController"/>.
    /// </summary>
    public AdminSupportController(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <summary>
    /// Retrieves a filtered, paginated list of all customer support tickets across the platform.
    /// </summary>
    [HttpGet("tickets")]
    [Authorize(Roles = "SuperAdmin,Auditor")]
    [ProducesResponseType(typeof(PagedResult<SupportTicketDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTickets(
        [FromQuery] SupportTicketStatus? status,
        [FromQuery] SupportTicketCategory? category,
        [FromQuery] SupportTicketPriority? priority,
        [FromQuery] bool? isSlaBreached,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new AdminGetSupportTicketsQuery(
            Status: status,
            Category: category,
            Priority: priority,
            IsSlaBreached: isSlaBreached,
            PageNumber: pageNumber,
            PageSize: pageSize), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Retrieves full ticket details and thread history for administrative investigation.
    /// </summary>
    [HttpGet("tickets/{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Auditor")]
    [ProducesResponseType(typeof(SupportTicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTicketById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSupportTicketByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates ticket status (e.g. InReview, Resolved, Closed, Escalated).
    /// </summary>
    [HttpPatch("tickets/{id:guid}/status")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(SupportTicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateTicketStatus(
        Guid id,
        [FromBody] UpdateTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new AdminUpdateTicketStatusCommand(
            TicketId: id,
            Status: request.Status,
            ResolutionSummary: request.ResolutionSummary), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Posts an administrative operator response message to the support ticket thread.
    /// </summary>
    [HttpPost("tickets/{id:guid}/messages")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(TicketMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddAdminMessage(
        Guid id,
        [FromBody] AddTicketMessageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new AdminAddTicketMessageCommand(id, request.Content), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves platform-wide customer support metrics, category distribution, and SLA performance.
    /// </summary>
    [HttpGet("reports")]
    [Authorize(Roles = "SuperAdmin,Auditor")]
    [ProducesResponseType(typeof(SupportReportsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReports(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSupportReportsQuery(fromUtc, toUtc), cancellationToken);
        return Ok(result);
    }
}
