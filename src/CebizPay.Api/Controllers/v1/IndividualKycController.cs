using Asp.Versioning;
using CebizPay.Application.UseCases.Individuals.GetKycDocuments;
using CebizPay.Application.UseCases.Individuals.SubmitKyc;
using CebizPay.Application.UseCases.Individuals.UpdateKycStatus;
using CebizPay.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Individual KYC management endpoints.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/individuals")]
public sealed class IndividualKycController : ControllerBase
{
    private readonly ISender _sender;

    /// <summary>
    /// Initializes a new instance of <see cref="IndividualKycController"/>.
    /// </summary>
    public IndividualKycController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Submits a KYC document for an individual user.
    /// </summary>
    [HttpPost("{id}/kyc-documents")]
    [Authorize]
    public async Task<IActionResult> SubmitKyc(
        [FromRoute] string id,
        [FromBody] SubmitKycRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SubmitKycCommand(id, request.DocumentType, request.DocumentNumber, request.DocumentUrl);
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Retrieves all KYC document submissions for an individual user.
    /// </summary>
    [HttpGet("{id}/kyc-documents")]
    [Authorize]
    public async Task<IActionResult> GetKycDocuments([FromRoute] string id, CancellationToken cancellationToken)
    {
        var query = new GetKycDocumentsQuery(id);
        var response = await _sender.Send(query, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Admin endpoint to update an individual's KYC status.
    /// </summary>
    [HttpPatch("{id}/kyc-status")]
    [Authorize(Policy = CebizPay.Application.Common.Security.AuthorizationPolicies.RequirePlatformAdmin)]
    public async Task<IActionResult> UpdateKycStatus(
        [FromRoute] string id,
        [FromBody] UpdateKycStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateKycStatusCommand(id, request.Status, request.AdminUserId, request.Reason);
        var response = await _sender.Send(command, cancellationToken);
        return Ok(response);
    }
}

/// <summary>Request DTO for SubmitKyc.</summary>
public sealed record SubmitKycRequest(
    DocumentType DocumentType,
    string DocumentNumber,
    string DocumentUrl);

/// <summary>Request DTO for UpdateKycStatus.</summary>
public sealed record UpdateKycStatusRequest(
    KycStatus Status,
    string AdminUserId,
    string? Reason);
