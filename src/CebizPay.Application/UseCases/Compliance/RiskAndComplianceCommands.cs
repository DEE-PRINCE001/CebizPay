#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Compliance.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Compliance;

// 1. Evaluate Risk
public sealed record EvaluateRiskCommand(
    RiskSubjectType SubjectType,
    string SubjectId,
    Guid? OrganizationId = null) : IRequest<RiskAssessmentResult>;

public sealed class EvaluateRiskCommandHandler : IRequestHandler<EvaluateRiskCommand, RiskAssessmentResult>
{
    private readonly IRiskEngine _riskEngine;
    private readonly ICddService _cddService;
    private readonly IComplianceDecisionService _decisionService;

    public EvaluateRiskCommandHandler(
        IRiskEngine riskEngine,
        ICddService cddService,
        IComplianceDecisionService decisionService)
    {
        _riskEngine = riskEngine;
        _cddService = cddService;
        _decisionService = decisionService;
    }

    public async Task<RiskAssessmentResult> Handle(EvaluateRiskCommand request, CancellationToken cancellationToken)
    {
        RiskAssessmentResult result;
        if (request.SubjectType == RiskSubjectType.Individual)
        {
            result = await _riskEngine.EvaluateIndividualRiskAsync(request.SubjectId, cancellationToken);
        }
        else if (request.SubjectType == RiskSubjectType.Organization)
        {
            if (!Guid.TryParse(request.SubjectId, out var orgId))
                throw new ArgumentException("SubjectId must be a valid Guid for Organization risk evaluation.", nameof(request));

            result = await _riskEngine.EvaluateOrganizationRiskAsync(orgId, cancellationToken);
        }
        else
        {
            throw new NotSupportedException($"SubjectType {request.SubjectType} not directly supported in bulk risk command.");
        }

        await _cddService.EvaluateCddAsync(request.SubjectType, request.SubjectId, request.OrganizationId, cancellationToken);
        await _decisionService.EvaluateDecisionAsync(request.SubjectType, request.SubjectId, request.OrganizationId, cancellationToken);

        return result;
    }
}

// 2. Request EDD Information
public sealed record RequestEddInformationCommand(
    Guid EddCaseId,
    string AdditionalRequirement) : IRequest<EddCaseDto>;

public sealed class RequestEddInformationCommandHandler : IRequestHandler<RequestEddInformationCommand, EddCaseDto>
{
    private readonly IEddWorkflowService _eddService;
    private readonly ICurrentUserService _currentUserService;

    public RequestEddInformationCommandHandler(IEddWorkflowService eddService, ICurrentUserService currentUserService)
    {
        _eddService = eddService;
        _currentUserService = currentUserService;
    }

    public async Task<EddCaseDto> Handle(RequestEddInformationCommand request, CancellationToken cancellationToken)
    {
        var adminUserId = _currentUserService.UserId ?? "System";
        return await _eddService.RequestEddInformationAsync(request.EddCaseId, request.AdditionalRequirement, adminUserId, cancellationToken);
    }
}

// 3. Submit EDD Information
public sealed record SubmitEddInformationCommand(
    Guid EddCaseId,
    string SubmittedInformation) : IRequest<EddCaseDto>;

public sealed class SubmitEddInformationCommandHandler : IRequestHandler<SubmitEddInformationCommand, EddCaseDto>
{
    private readonly IEddWorkflowService _eddService;
    private readonly ICurrentUserService _currentUserService;

    public SubmitEddInformationCommandHandler(IEddWorkflowService eddService, ICurrentUserService currentUserService)
    {
        _eddService = eddService;
        _currentUserService = currentUserService;
    }

    public async Task<EddCaseDto> Handle(SubmitEddInformationCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId ?? "Anonymous";
        return await _eddService.SubmitEddInformationAsync(request.EddCaseId, request.SubmittedInformation, userId, cancellationToken);
    }
}

// 4. Assign EDD Reviewer
public sealed record AssignEddReviewerCommand(
    Guid EddCaseId,
    string ReviewerAdminUserId) : IRequest<EddCaseDto>;

public sealed class AssignEddReviewerCommandHandler : IRequestHandler<AssignEddReviewerCommand, EddCaseDto>
{
    private readonly IEddWorkflowService _eddService;

    public AssignEddReviewerCommandHandler(IEddWorkflowService eddService)
    {
        _eddService = eddService;
    }

    public async Task<EddCaseDto> Handle(AssignEddReviewerCommand request, CancellationToken cancellationToken)
    {
        return await _eddService.AssignReviewerAsync(request.EddCaseId, request.ReviewerAdminUserId, cancellationToken);
    }
}

// 5. Approve EDD Case
public sealed record ApproveEddCaseCommand(
    Guid EddCaseId,
    string Reason,
    bool IsSeniorManagement = false) : IRequest<EddCaseDto>;

public sealed class ApproveEddCaseCommandHandler : IRequestHandler<ApproveEddCaseCommand, EddCaseDto>
{
    private readonly IEddWorkflowService _eddService;
    private readonly ICurrentUserService _currentUserService;

    public ApproveEddCaseCommandHandler(IEddWorkflowService eddService, ICurrentUserService currentUserService)
    {
        _eddService = eddService;
        _currentUserService = currentUserService;
    }

    public async Task<EddCaseDto> Handle(ApproveEddCaseCommand request, CancellationToken cancellationToken)
    {
        var adminUserId = _currentUserService.UserId ?? "System";
        return await _eddService.ApproveEddCaseAsync(request.EddCaseId, request.Reason, adminUserId, request.IsSeniorManagement, cancellationToken);
    }
}

// 6. Reject EDD Case
public sealed record RejectEddCaseCommand(
    Guid EddCaseId,
    string Reason) : IRequest<EddCaseDto>;

public sealed class RejectEddCaseCommandHandler : IRequestHandler<RejectEddCaseCommand, EddCaseDto>
{
    private readonly IEddWorkflowService _eddService;
    private readonly ICurrentUserService _currentUserService;

    public RejectEddCaseCommandHandler(IEddWorkflowService eddService, ICurrentUserService currentUserService)
    {
        _eddService = eddService;
        _currentUserService = currentUserService;
    }

    public async Task<EddCaseDto> Handle(RejectEddCaseCommand request, CancellationToken cancellationToken)
    {
        var adminUserId = _currentUserService.UserId ?? "System";
        return await _eddService.RejectEddCaseAsync(request.EddCaseId, request.Reason, adminUserId, cancellationToken);
    }
}

// 7. Apply Compliance Override
public sealed record ApplyComplianceOverrideCommand(
    RiskSubjectType SubjectType,
    string SubjectId,
    ComplianceDecisionType NewDecision,
    string Reason,
    Guid? OrganizationId = null) : IRequest<ComplianceDecisionDto>;

public sealed class ApplyComplianceOverrideCommandHandler : IRequestHandler<ApplyComplianceOverrideCommand, ComplianceDecisionDto>
{
    private readonly IComplianceDecisionService _decisionService;
    private readonly ICurrentUserService _currentUserService;

    public ApplyComplianceOverrideCommandHandler(
        IComplianceDecisionService decisionService,
        ICurrentUserService currentUserService)
    {
        _decisionService = decisionService;
        _currentUserService = currentUserService;
    }

    public async Task<ComplianceDecisionDto> Handle(ApplyComplianceOverrideCommand request, CancellationToken cancellationToken)
    {
        var adminUserId = _currentUserService.UserId ?? "System";
        return await _decisionService.ApplyManualOverrideAsync(
            request.SubjectType,
            request.SubjectId,
            request.NewDecision,
            request.Reason,
            adminUserId,
            request.OrganizationId,
            cancellationToken);
    }
}

// 8. Place Compliance Restriction
public sealed record PlaceComplianceRestrictionCommand(
    RiskSubjectType SubjectType,
    string SubjectId,
    ComplianceRestrictionType RestrictionType,
    string Reason,
    decimal? DailyCapAmount = null,
    decimal? SingleCapAmount = null,
    Guid? OrganizationId = null) : IRequest<ComplianceRestrictionDto>;

public sealed class PlaceComplianceRestrictionCommandHandler : IRequestHandler<PlaceComplianceRestrictionCommand, ComplianceRestrictionDto>
{
    private readonly IComplianceRestrictionService _restrictionService;
    private readonly ICurrentUserService _currentUserService;

    public PlaceComplianceRestrictionCommandHandler(
        IComplianceRestrictionService restrictionService,
        ICurrentUserService currentUserService)
    {
        _restrictionService = restrictionService;
        _currentUserService = currentUserService;
    }

    public async Task<ComplianceRestrictionDto> Handle(PlaceComplianceRestrictionCommand request, CancellationToken cancellationToken)
    {
        var placedBy = _currentUserService.UserId ?? "System";
        return await _restrictionService.PlaceRestrictionAsync(
            request.SubjectType,
            request.SubjectId,
            request.RestrictionType,
            request.Reason,
            placedBy,
            request.DailyCapAmount,
            request.SingleCapAmount,
            request.OrganizationId,
            cancellationToken);
    }
}

// 9. Release Compliance Restriction
public sealed record ReleaseComplianceRestrictionCommand(
    Guid RestrictionId,
    string ReleaseReason) : IRequest<ComplianceRestrictionDto>;

public sealed class ReleaseComplianceRestrictionCommandHandler : IRequestHandler<ReleaseComplianceRestrictionCommand, ComplianceRestrictionDto>
{
    private readonly IComplianceRestrictionService _restrictionService;
    private readonly ICurrentUserService _currentUserService;

    public ReleaseComplianceRestrictionCommandHandler(
        IComplianceRestrictionService restrictionService,
        ICurrentUserService currentUserService)
    {
        _restrictionService = restrictionService;
        _currentUserService = currentUserService;
    }

    public async Task<ComplianceRestrictionDto> Handle(ReleaseComplianceRestrictionCommand request, CancellationToken cancellationToken)
    {
        var releasedBy = _currentUserService.UserId ?? "System";
        return await _restrictionService.ReleaseRestrictionAsync(
            request.RestrictionId,
            request.ReleaseReason,
            releasedBy,
            cancellationToken);
    }
}
