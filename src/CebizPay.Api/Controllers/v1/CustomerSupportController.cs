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
/// Customer-facing endpoints for Kola chatbot triage and support ticket management.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/support")]
[Authorize]
public sealed class CustomerSupportController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="CustomerSupportController"/>.
    /// </summary>
    public CustomerSupportController(ISender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    /// <summary>
    /// Initiates a new Kola support triage session.
    /// </summary>
    [HttpPost("kola/session")]
    [ProducesResponseType(typeof(Application.Common.Interfaces.Support.KolaSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> StartKolaSession(
        [FromBody] KolaStartSessionRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new StartKolaSessionCommand(request?.OrganizationId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Interacts with an active Kola triage session.
    /// </summary>
    [HttpPost("kola/message")]
    [ProducesResponseType(typeof(Application.Common.Interfaces.Support.KolaSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> InteractKolaSession(
        [FromBody] KolaInteractRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new InteractKolaSessionCommand(
            SessionId: request.SessionId,
            CurrentState: request.CurrentState,
            Category: request.Category,
            SelectedIssueIndex: request.SelectedIssueIndex,
            Message: request.Message,
            OrganizationId: request.OrganizationId), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Opens a new customer support ticket (supports direct and offline synchronized submissions).
    /// </summary>
    [HttpPost("tickets")]
    [ProducesResponseType(typeof(SupportTicketDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateTicket(
        [FromBody] CreateSupportTicketRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateSupportTicketCommand(
            Category: request.Category,
            Subject: request.Subject,
            Description: request.Description,
            Priority: request.Priority,
            IdempotencyKey: request.IdempotencyKey), cancellationToken);

        return CreatedAtAction(nameof(GetTicketById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Retrieves a paginated list of support tickets owned by the authenticated customer.
    /// </summary>
    [HttpGet("tickets")]
    [ProducesResponseType(typeof(PagedResult<SupportTicketDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyTickets(
        [FromQuery] SupportTicketStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetSupportTicketsQuery(status, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves ticket details and thread messages with strict ownership validation.
    /// </summary>
    [HttpGet("tickets/{id:guid}")]
    [ProducesResponseType(typeof(SupportTicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTicketById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSupportTicketByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Appends a new message to the customer's support ticket thread.
    /// </summary>
    [HttpPost("tickets/{id:guid}/messages")]
    [ProducesResponseType(typeof(TicketMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddMessage(
        Guid id,
        [FromBody] AddTicketMessageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new AddTicketMessageCommand(id, request.Content), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Customer closes their own support ticket.
    /// </summary>
    [HttpPost("tickets/{id:guid}/close")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CloseTicket(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _sender.Send(new CloseSupportTicketCommand(id), cancellationToken);
        return Ok(new { Message = "Support ticket closed successfully." });
    }
}
