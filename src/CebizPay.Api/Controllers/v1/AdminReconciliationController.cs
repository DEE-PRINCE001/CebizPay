#pragma warning disable CS1591
using System.Security.Claims;
using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Application.UseCases.Reconciliation;
using CebizPay.Domain.Payments.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Administrative APIs for managing financial and compliance reconciliation, status requeries,
/// event reprocessing, and manual review dispositions without unrestricted financial bypasses.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/reconciliation")]
[Authorize(Roles = "SuperAdmin,Admin,Auditor")]
public sealed class AdminReconciliationController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminReconciliationController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Retrieves paginated reconciliation records with optional rail, provider, and status filters.
    /// </summary>
    [HttpGet("records")]
    [ProducesResponseType(typeof(IReadOnlyList<ReconciliationRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecords(
        [FromQuery] ReconciliationType? type,
        [FromQuery] ReconciliationStatus? status,
        [FromQuery] string? provider,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetReconciliationRecordsQuery(type, status, provider, pageNumber, pageSize),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Retrieves paginated list of outstanding financial recoveries owed by account holders.
    /// </summary>
    [HttpGet("recoveries")]
    [ProducesResponseType(typeof(IReadOnlyList<RecoveryOutstandingRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecoveries(
        [FromQuery] Guid? walletId,
        [FromQuery] RecoveryStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetOutstandingRecoveriesQuery(walletId, status, pageNumber, pageSize),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Triggers an on-demand external provider status requery for any transaction or verification reference.
    /// </summary>
    [HttpPost("requery")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(UnifiedReconciliationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequeryStatus(
        [FromBody] RequeryStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Reference))
            return BadRequest(new { message = "Reference is required." });

        var result = await _mediator.Send(new RequeryPaymentStatusCommand(request.Reference), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retries processing of a failed or dead-lettered durable webhook event.
    /// </summary>
    [HttpPost("events/{eventId:guid}/retry")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RetryWebhook(
        [FromRoute] Guid eventId,
        [FromQuery] bool isCompliance = false,
        CancellationToken cancellationToken = default)
    {
        var success = await _mediator.Send(new RetryWebhookEventCommand(eventId, isCompliance), cancellationToken);
        return Ok(new { success, eventId, isCompliance });
    }

    /// <summary>
    /// Submits an authorized manual review disposition for an unresolved reconciliation record.
    /// </summary>
    [HttpPost("records/{recordId:guid}/review")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ProducesResponseType(typeof(UnifiedReconciliationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitManualReview(
        [FromRoute] Guid recordId,
        [FromBody] SubmitReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ReviewerNotes))
            return BadRequest(new { message = "ReviewerNotes are required." });

        var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "ADMIN";

        var result = await _mediator.Send(new SubmitManualReviewDecisionCommand(
            recordId,
            request.Decision,
            request.ReviewerNotes,
            actorUserId), cancellationToken);

        return Ok(result);
    }
}

public sealed record RequeryStatusRequest(string Reference);

public sealed record SubmitReviewRequest(
    ManualReviewDecision Decision,
    string ReviewerNotes);
