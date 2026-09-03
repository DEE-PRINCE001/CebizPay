using Asp.Versioning;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Announcements;
using CebizPay.Domain.Communication.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Authoritative API endpoints for Platform-wide and tenant-isolated Workplace announcements.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/announcements")]
[Authorize]
public sealed class AnnouncementsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="AnnouncementsController"/>.
    /// </summary>
    public AnnouncementsController(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <summary>
    /// Creates a new announcement (either global Platform or tenant-isolated Workplace).
    /// </summary>
    /// <param name="request">Announcement creation details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAnnouncement(
        [FromBody] CreateAnnouncementRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateAnnouncementCommand(
            request.Scope,
            request.Title,
            request.Description,
            request.PublishImmediately);

        var result = await _sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetAnnouncementById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Retrieves a paginated feed of published platform-wide announcements.
    /// Surfaced on user home dashboards. Excludes all workplace announcements.
    /// </summary>
    [HttpGet("platform")]
    [ProducesResponseType(typeof(PagedResult<AnnouncementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPlatformAnnouncements(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPlatformAnnouncementsQuery(pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a paginated feed of published workplace announcements for the caller's active organization.
    /// Surfaced on user work dashboards. Excludes all platform and other organizations' announcements.
    /// </summary>
    [HttpGet("workplace")]
    [ProducesResponseType(typeof(PagedResult<AnnouncementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetWorkplaceAnnouncements(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetWorkplaceAnnouncementsQuery(pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a single announcement by ID with strict tenant isolation and visibility checks.
    /// </summary>
    /// <param name="id">Announcement identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnnouncementById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetAnnouncementByIdQuery(id);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Publishes a draft announcement.
    /// </summary>
    /// <param name="id">Announcement identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishAnnouncement(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new PublishAnnouncementCommand(id);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Archives an announcement, permanently hiding it from public and workplace feeds.
    /// </summary>
    /// <param name="id">Announcement identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveAnnouncement(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ArchiveAnnouncementCommand(id);
        await _sender.Send(command, cancellationToken);
        return Ok(new { Succeeded = true, Message = "Announcement archived successfully." });
    }

    /// <summary>
    /// Deletes / archives an announcement.
    /// </summary>
    /// <param name="id">Announcement identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAnnouncement(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ArchiveAnnouncementCommand(id);
        await _sender.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Administrative management directory for announcements with scope, status, and text search filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AnnouncementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAnnouncementsDirectory(
        [FromQuery] AnnouncementScope? scope,
        [FromQuery] AnnouncementStatus? status,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAnnouncementsDirectoryQuery(scope, status, search, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }
}
