using Asp.Versioning;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Authoritative API endpoints for the user notification inbox, unread counts,
/// FCM device token registration, and notification delivery channel preferences.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="NotificationsController"/>.
    /// </summary>
    public NotificationsController(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <summary>
    /// Retrieves a paginated list of in-app notifications for the authenticated user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<InAppNotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool? isRead,
        [FromQuery] Guid? organizationId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetNotificationsQuery(isRead, organizationId, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves the total count of unread in-app notifications for the authenticated user.
    /// </summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var query = new GetUnreadNotificationCountQuery();
        var count = await _sender.Send(query, cancellationToken);
        return Ok(new { Count = count });
    }

    /// <summary>
    /// Marks a specific in-app notification as read.
    /// </summary>
    [HttpPatch("{id:guid}/read")]
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new MarkNotificationReadCommand(id);
        await _sender.Send(command, cancellationToken);
        return Ok(new { Succeeded = true, Message = "Notification marked as read." });
    }

    /// <summary>
    /// Marks all unread in-app notifications for the authenticated user as read.
    /// </summary>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var command = new MarkAllNotificationsReadCommand();
        var count = await _sender.Send(command, cancellationToken);
        return Ok(new { Succeeded = true, Count = count, Message = "All notifications marked as read." });
    }

    /// <summary>
    /// Registers an FCM device token for authenticated push notifications.
    /// </summary>
    [HttpPost("devices")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterDevice(
        [FromBody] RegisterDeviceTokenRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterDeviceTokenCommand(request.Token, request.Platform, request.DeviceModel);
        await _sender.Send(command, cancellationToken);
        return Ok(new { Succeeded = true, Message = "Device token registered successfully." });
    }

    /// <summary>
    /// Deactivates a registered FCM device token on logout or device removal.
    /// </summary>
    [HttpDelete("devices/{token}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeactivateDevice(
        [FromRoute] string token,
        CancellationToken cancellationToken)
    {
        var command = new DeactivateDeviceTokenCommand(token);
        await _sender.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Retrieves user notification delivery channel preferences across all categories.
    /// </summary>
    [HttpGet("preferences")]
    [ProducesResponseType(typeof(List<NotificationPreferenceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken)
    {
        var query = new GetNotificationPreferencesQuery();
        var result = await _sender.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates user notification delivery channel preferences.
    /// Critical security and compliance notifications cannot be disabled.
    /// </summary>
    [HttpPut("preferences")]
    [ProducesResponseType(typeof(List<NotificationPreferenceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateNotificationPreferencesCommand(request.Preferences);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(result);
    }
}
