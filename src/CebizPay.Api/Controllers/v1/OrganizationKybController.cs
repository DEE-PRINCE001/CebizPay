using Asp.Versioning;
using CebizPay.Application.UseCases.Organizations.Kyb;
using CebizPay.Application.UseCases.Organizations.RegisterStep1;
using CebizPay.Application.UseCases.Organizations.RegisterStep2;
using CebizPay.Application.UseCases.Organizations.UpdateStatus;
using CebizPay.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    /// Real-time Corporate Affairs Commission (CAC) business registry lookup and director inquiry.
    /// </summary>
    [HttpPost("org/kyb/lookup-cac")]
    [Authorize]
    [ProducesResponseType(typeof(CacLookupResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> LookupCac([FromBody] LookupCacRequest request, CancellationToken cancellationToken)
    {
        var command = new LookupCacCommand(request.OrganizationId, request.CacNumber, request.CompanyName);
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Uploads a KYB verification document to secure Cloudinary storage.
    /// </summary>
    [HttpPost("org/kyb/documents")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(KybDocumentUploadResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadDocument([FromForm] UploadKybDocumentRequest request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(new { code = "INVALID_FILE", message = "A valid file is required." });
        }

        using var stream = request.File.OpenReadStream();
        var command = new UploadKybDocumentCommand(
            OrganizationId: request.OrganizationId,
            DocumentType: request.DocumentType,
            FileStream: stream,
            FileName: request.File.FileName,
            ContentType: request.File.ContentType,
            FileSizeBytes: request.File.Length);

        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Updates organization status (Admin lifecycle transition).
    /// </summary>
    [HttpPatch("organizations/{id:guid}/status")]
    [Authorize(Policy = CebizPay.Application.Common.Security.AuthorizationPolicies.RequirePlatformAdmin)]
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
/// Request model for CAC business lookup.
/// </summary>
public sealed record LookupCacRequest(
    Guid OrganizationId,
    string CacNumber,
    string? CompanyName = null);

/// <summary>
/// Form request model for multipart KYB document uploads.
/// </summary>
public sealed class UploadKybDocumentRequest
{
    /// <summary>Target organization ID.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Type of document being uploaded (e.g. CacCertificate, StatusReport, Memorandum, ProofOfAddress, Logo).</summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>Multipart file content.</summary>
    public IFormFile File { get; set; } = null!;
}

/// <summary>
/// Request DTO for updating organization status.
/// </summary>
public sealed record UpdateOrganizationStatusRequest(
    OrganizationStatus Status,
    string? Reason);
