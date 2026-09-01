#pragma warning disable CS1591
using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Models;
using CebizPay.Application.UseCases.Compliance;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Provider-neutral compliance verification APIs for Individual KYC and Corporate KYB.
/// In accordance with CBN Customer Due Diligence regulations, individual tiered KYC is separate from legal person KYB.
/// External provider checks produce neutral evidence and do not automatically constitute final CebizPay compliance approval.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance")]
[Authorize]
public sealed class ComplianceController : ControllerBase
{
    private readonly IMediator _mediator;

    public ComplianceController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Verifies an individual's Bank Verification Number (BVN) against official NIBSS registry records.
    /// </summary>
    [HttpPost("kyc/bvn")]
    [ProducesResponseType(typeof(VerificationOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyBvn(
        [FromBody] VerifyBvnRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new VerifyBvnCommand(
            request.Bvn,
            request.FirstName,
            request.LastName,
            request.DateOfBirth,
            idempotencyKey,
            request.UserId);

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Verifies an individual's National Identification Number (NIN) against official NIMC registry records.
    /// </summary>
    [HttpPost("kyc/nin")]
    [ProducesResponseType(typeof(VerificationOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyNin(
        [FromBody] VerifyNinRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new VerifyNinCommand(
            request.Nin,
            request.FirstName,
            request.LastName,
            request.DateOfBirth,
            idempotencyKey,
            request.UserId);

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Performs biometric liveness detection and 1:1 facial matching against a reference ID photo.
    /// </summary>
    [HttpPost("kyc/biometrics")]
    [ProducesResponseType(typeof(VerificationOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyBiometrics(
        [FromBody] VerifyBiometricsRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new VerifyBiometricsCommand(
            request.SelfieImageBase64,
            request.ReferenceImageBase64,
            request.IdNumber,
            idempotencyKey,
            request.UserId);

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Performs OCR and authenticity validation for a government-issued identity document.
    /// </summary>
    [HttpPost("kyc/document")]
    [ProducesResponseType(typeof(VerificationOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyDocument(
        [FromBody] VerifyDocumentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new VerifyDocumentCommand(
            request.DocumentType,
            request.DocumentNumber,
            request.DocumentImageBase64,
            request.FirstName,
            request.LastName,
            idempotencyKey,
            request.UserId);

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Screens an individual or entity against global AML, PEP, and sanctions watchlists.
    /// </summary>
    [HttpPost("kyc/aml")]
    [ProducesResponseType(typeof(VerificationOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ScreenAml(
        [FromBody] ScreenAmlRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new ScreenAmlCommand(
            request.Name,
            request.IsEntity,
            request.OrganizationId,
            request.RegistrationNumber,
            request.DateOfBirth,
            request.CountryCode,
            idempotencyKey,
            request.UserId);

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Verifies corporate legal entity registration status with the Corporate Affairs Commission (CAC).
    /// </summary>
    [HttpPost("kyb/business")]
    [ProducesResponseType(typeof(VerificationOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyBusiness(
        [FromBody] VerifyBusinessRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new VerifyBusinessCommand(
            request.OrganizationId,
            request.CacNumber,
            request.CompanyName,
            idempotencyKey);

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Queries verified corporate directors and ultimate beneficial owners (UBOs) for an organization.
    /// </summary>
    [HttpPost("kyb/beneficial-owners")]
    [ProducesResponseType(typeof(VerificationOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBeneficialOwners(
        [FromBody] GetBeneficialOwnersRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new GetBeneficialOwnersCommand(
            request.OrganizationId,
            request.CacNumber,
            idempotencyKey);

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a verification operation and its immutable evidence collection by internal reference.
    /// </summary>
    [HttpGet("operations/{reference}")]
    [ProducesResponseType(typeof(VerificationOperationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOperationByReference(
        [FromRoute] string reference,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetVerificationOperationByReferenceQuery(reference), cancellationToken);
        if (result == null)
            return NotFound(new { status = "error", message = "Verification operation not found." });

        return Ok(result);
    }

    /// <summary>
    /// Queries historical verification evidence records.
    /// </summary>
    [HttpGet("evidence")]
    [ProducesResponseType(typeof(PagedResult<VerificationEvidenceSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvidence(
        [FromQuery] string? userId,
        [FromQuery] Guid? organizationId,
        [FromQuery] VerificationCapability? capability,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetVerificationEvidenceQuery(userId, organizationId, capability, pageNumber, pageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves the caller's Customer Due Diligence (CDD) profile, KYC tier, active compliance decision, and active restrictions.
    /// </summary>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(ComplianceProfileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(
        [FromQuery] RiskSubjectType subjectType = RiskSubjectType.Individual,
        [FromQuery] string? subjectId = null,
        [FromQuery] Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        var targetSubjectId = subjectId ?? User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
        var query = new GetComplianceProfileQuery(subjectType, targetSubjectId, organizationId);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves the caller's current risk assessment and explainable factor findings.
    /// </summary>
    [HttpGet("risk")]
    [ProducesResponseType(typeof(RiskAssessmentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRisk(
        [FromQuery] RiskSubjectType subjectType = RiskSubjectType.Individual,
        [FromQuery] string? subjectId = null,
        [FromQuery] Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        var targetSubjectId = subjectId ?? User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
        var query = new GetRiskAssessmentQuery(subjectType, targetSubjectId, organizationId);
        var result = await _mediator.Send(query, cancellationToken);
        if (result == null)
            return NotFound(new { status = "error", message = "No risk assessment found." });

        return Ok(result);
    }

    /// <summary>
    /// Retrieves the caller's historical risk assessment log.
    /// </summary>
    [HttpGet("risk/history")]
    [ProducesResponseType(typeof(IReadOnlyList<RiskAssessmentResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRiskHistory(
        [FromQuery] RiskSubjectType subjectType = RiskSubjectType.Individual,
        [FromQuery] string? subjectId = null,
        CancellationToken cancellationToken = default)
    {
        var targetSubjectId = subjectId ?? User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
        var query = new GetRiskHistoryQuery(subjectType, targetSubjectId);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves details of an assigned Enhanced Due Diligence (EDD) case.
    /// </summary>
    [HttpGet("edd/{id:guid}")]
    [ProducesResponseType(typeof(EddCaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEddCase(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetEddCaseByIdQuery(id), cancellationToken);
        if (result == null)
            return NotFound(new { status = "error", message = "EDD case not found." });

        return Ok(result);
    }

    /// <summary>
    /// Submits requested documentation or narrative for an active Enhanced Due Diligence (EDD) case.
    /// </summary>
    [HttpPost("edd/{id:guid}/submit")]
    [ProducesResponseType(typeof(EddCaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitEddInformation(
        [FromRoute] Guid id,
        [FromBody] SubmitEddInformationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SubmitEddInformationCommand(id, request.SubmittedInformation);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Evaluates transaction compliance eligibility before executing a financial operation (payout, transfer, funding).
    /// </summary>
    [HttpPost("eligibility/check")]
    [ProducesResponseType(typeof(TransactionEligibilityResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckEligibility(
        [FromBody] CheckEligibilityRequest request,
        CancellationToken cancellationToken)
    {
        var userId = request.UserId ?? User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
        var query = new CheckTransactionEligibilityQuery(userId, request.OrganizationId, request.OperationType, request.Amount, request.Currency);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}

public sealed record VerifyBvnRequest(
    string Bvn,
    string FirstName,
    string LastName,
    DateTime? DateOfBirth = null,
    string? UserId = null);

public sealed record VerifyNinRequest(
    string Nin,
    string FirstName,
    string LastName,
    DateTime? DateOfBirth = null,
    string? UserId = null);

public sealed record VerifyBiometricsRequest(
    string SelfieImageBase64,
    string? ReferenceImageBase64 = null,
    string? IdNumber = null,
    string? UserId = null);

public sealed record VerifyDocumentRequest(
    DocumentType DocumentType,
    string DocumentNumber,
    string DocumentImageBase64,
    string? FirstName = null,
    string? LastName = null,
    string? UserId = null);

public sealed record ScreenAmlRequest(
    string Name,
    bool IsEntity = false,
    Guid? OrganizationId = null,
    string? RegistrationNumber = null,
    DateTime? DateOfBirth = null,
    string? CountryCode = "NG",
    string? UserId = null);

public sealed record VerifyBusinessRequest(
    Guid OrganizationId,
    string CacNumber,
    string CompanyName);

public sealed record GetBeneficialOwnersRequest(
    Guid OrganizationId,
    string CacNumber);

public sealed record SubmitEddInformationRequest(
    string SubmittedInformation);

public sealed record CheckEligibilityRequest(
    ComplianceOperationType OperationType,
    decimal Amount,
    Currency Currency,
    string? UserId = null,
    Guid? OrganizationId = null);
