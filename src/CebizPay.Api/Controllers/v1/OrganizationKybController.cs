using Asp.Versioning;
using CebizPay.Application.UseCases.Organizations.RegisterStep1;
using CebizPay.Application.UseCases.Organizations.RegisterStep2;
using CebizPay.Application.UseCases.Organizations.UpdateStatus;
using CebizPay.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Organization KYB &amp; Status management endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
public sealed class OrganizationKybController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="OrganizationKybController"/>.
    /// </summary>
    public OrganizationKybController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Step 1 Organization KYB registration.
    /// </summary>
    [HttpPost("org/kyb/register-step1")]
    [Authorize]
    public async Task<IActionResult> RegisterStep1([FromBody] RegisterStep1Command command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Step 2 Organization KYB registration.
    /// </summary>
    [HttpPost("org/kyb/register-step2")]
    [Authorize]
    public async Task<IActionResult> RegisterStep2([FromBody] RegisterStep2Command command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Updates organization status (Admin lifecycle transition).
    /// </summary>
    [HttpPatch("organizations/{id:guid}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateOrganizationStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateOrganizationStatusCommand(id, request.Status, request.Reason);
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }
}

/// <summary>
/// Request DTO for updating organization status.
/// </summary>
public sealed record UpdateOrganizationStatusRequest(
    OrganizationStatus Status,
    string? Reason);
