#pragma warning disable CS1591
using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.UseCases.Compliance;
using CebizPay.Domain.Compliance.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// Administrative compliance APIs for compliance officers and platform risk managers.
/// Provides EDD case management, on-demand risk reassessment, tightly permissioned manual overrides, and account restrictions.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/compliance")]
[Authorize(Roles = "SuperAdmin,Admin,Auditor")]
public sealed class AdminComplianceController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminComplianceController(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Retrieves current active risk assessment and explainable factor findings for an individual or organization.
    /// </summary>
    [HttpGet("assessments/{subjectType}/{subjectId}")]
    [ProducesResponseType(typeof(RiskAssessmentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssessment(
        [FromRoute] RiskSubjectType subjectType,
        [FromRoute] string subjectId,
        [FromQuery] Guid? organizationId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetRiskAssessmentQuery(subjectType, subjectId, organizationId), cancellationToken);
        if (result == null)
            return NotFound(new { status = "error", message = "No risk assessment found for subject." });

        return Ok(result);
    }

    /// <summary>
    /// Retrieves full immutable risk assessment audit history for a subject.
    /// </summary>
    [HttpGet("assessments/{subjectType}/{subjectId}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<RiskAssessmentResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssessmentHistory(
        [FromRoute] RiskSubjectType subjectType,
        [FromRoute] string subjectId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetRiskHistoryQuery(subjectType, subjectId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Triggers an on-demand risk reassessment for an individual or organization.
    /// </summary>
    [HttpPost("assessments/evaluate")]
    [ProducesResponseType(typeof(RiskAssessmentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EvaluateRisk(
        [FromBody] EvaluateRiskRequest request,
        CancellationToken cancellationToken)
    {
        var command = new EvaluateRiskCommand(request.SubjectType, request.SubjectId, request.OrganizationId);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Queries Enhanced Due Diligence (EDD) cases with optional status/type filters.
    /// </summary>
    [HttpGet("edd/cases")]
    [ProducesResponseType(typeof(IReadOnlyList<EddCaseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEddCases(
        [FromQuery] EddStatus? status,
        [FromQuery] RiskSubjectType? subjectType,
        [FromQuery] Guid? organizationId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetEddCasesQuery(status, subjectType, organizationId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves details of an Enhanced Due Diligence (EDD) case.
    /// </summary>
    [HttpGet("edd/cases/{id:guid}")]
    [ProducesResponseType(typeof(EddCaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEddCaseById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetEddCaseByIdQuery(id), cancellationToken);
        if (result == null)
            return NotFound(new { status = "error", message = "EDD case not found." });

        return Ok(result);
    }

    /// <summary>
    /// Requests additional documentation or information from a customer for an EDD case.
    /// </summary>
    [HttpPost("edd/cases/{id:guid}/request-information")]
    [ProducesResponseType(typeof(EddCaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestEddInformation(
        [FromRoute] Guid id,
        [FromBody] AdminRequestEddInformationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RequestEddInformationCommand(id, request.AdditionalRequirement);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Assigns a compliance officer investigator to an EDD case.
    /// </summary>
    [HttpPost("edd/cases/{id:guid}/assign")]
    [ProducesResponseType(typeof(EddCaseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssignEddReviewer(
        [FromRoute] Guid id,
        [FromBody] AssignEddReviewerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AssignEddReviewerCommand(id, request.ReviewerAdminUserId);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Approves an Enhanced Due Diligence (EDD) case.
    /// Enforces Senior Management authorization where required by regulation.
    /// </summary>
    [HttpPost("edd/cases/{id:guid}/approve")]
    [ProducesResponseType(typeof(EddCaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveEddCase(
        [FromRoute] Guid id,
        [FromBody] ApproveEddCaseRequest request,
        CancellationToken cancellationToken)
    {
        var isSeniorManagement = User.IsInRole("SuperAdmin") || request.IsSeniorManagement;
        var command = new ApproveEddCaseCommand(id, request.Reason, isSeniorManagement);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Rejects an Enhanced Due Diligence (EDD) case with formal justification.
    /// </summary>
    [HttpPost("edd/cases/{id:guid}/reject")]
    [ProducesResponseType(typeof(EddCaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectEddCase(
        [FromRoute] Guid id,
        [FromBody] RejectEddCaseRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RejectEddCaseCommand(id, request.Reason);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Applies a tightly permissioned administrative manual override to a compliance decision.
    /// Non-negotiable regulatory safeguards (e.g. active sanctions match) cannot be bypassed.
    /// </summary>
    [HttpPost("decisions/override")]
    [ProducesResponseType(typeof(ComplianceDecisionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApplyComplianceOverride(
        [FromBody] ApplyComplianceOverrideRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ApplyComplianceOverrideCommand(
            request.SubjectType,
            request.SubjectId,
            request.NewDecision,
            request.Reason,
            request.OrganizationId);

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Places an active operational or financial volume restriction on an account.
    /// </summary>
    [HttpPost("restrictions")]
    [ProducesResponseType(typeof(ComplianceRestrictionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PlaceRestriction(
        [FromBody] PlaceRestrictionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new PlaceComplianceRestrictionCommand(
            request.SubjectType,
            request.SubjectId,
            request.RestrictionType,
            request.Reason,
            request.DailyCapAmount,
            request.SingleCapAmount,
            request.OrganizationId);

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Releases an active compliance restriction with mandatory justification.
    /// </summary>
    [HttpPost("restrictions/{id:guid}/release")]
    [ProducesResponseType(typeof(ComplianceRestrictionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReleaseRestriction(
        [FromRoute] Guid id,
        [FromBody] ReleaseRestrictionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ReleaseComplianceRestrictionCommand(id, request.ReleaseReason);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}

public sealed record EvaluateRiskRequest(
    RiskSubjectType SubjectType,
    string SubjectId,
    Guid? OrganizationId = null);

public sealed record AdminRequestEddInformationRequest(
    string AdditionalRequirement);

public sealed record AssignEddReviewerRequest(
    string ReviewerAdminUserId);

public sealed record ApproveEddCaseRequest(
    string Reason,
    bool IsSeniorManagement = false);

public sealed record RejectEddCaseRequest(
    string Reason);

public sealed record ApplyComplianceOverrideRequest(
    RiskSubjectType SubjectType,
    string SubjectId,
    ComplianceDecisionType NewDecision,
    string Reason,
    Guid? OrganizationId = null);

public sealed record PlaceRestrictionRequest(
    RiskSubjectType SubjectType,
    string SubjectId,
    ComplianceRestrictionType RestrictionType,
    string Reason,
    decimal? DailyCapAmount = null,
    decimal? SingleCapAmount = null,
    Guid? OrganizationId = null);

public sealed record ReleaseRestrictionRequest(
    string ReleaseReason);
