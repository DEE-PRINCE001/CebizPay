#pragma warning disable CS1591
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Finance.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Compliance;

// 1. Get Compliance Profile
public sealed record GetComplianceProfileQuery(
    RiskSubjectType SubjectType,
    string SubjectId,
    Guid? OrganizationId = null) : IRequest<ComplianceProfileResponse>;

public sealed record ComplianceProfileResponse(
    CddProfileDto CddProfile,
    ComplianceDecisionDto? CurrentDecision,
    IReadOnlyList<ComplianceRestrictionDto> ActiveRestrictions);

public sealed class GetComplianceProfileQueryHandler : IRequestHandler<GetComplianceProfileQuery, ComplianceProfileResponse>
{
    private readonly ICddService _cddService;
    private readonly IComplianceDecisionService _decisionService;
    private readonly IComplianceRestrictionService _restrictionService;

    public GetComplianceProfileQueryHandler(
        ICddService cddService,
        IComplianceDecisionService decisionService,
        IComplianceRestrictionService restrictionService)
    {
        _cddService = cddService;
        _decisionService = decisionService;
        _restrictionService = restrictionService;
    }

    public async Task<ComplianceProfileResponse> Handle(GetComplianceProfileQuery request, CancellationToken cancellationToken)
    {
        var cdd = await _cddService.GetOrCreateCddProfileAsync(request.SubjectType, request.SubjectId, request.OrganizationId, cancellationToken);
        var decision = await _decisionService.EvaluateDecisionAsync(request.SubjectType, request.SubjectId, request.OrganizationId, cancellationToken);
        var restrictions = await _restrictionService.GetActiveRestrictionsAsync(request.SubjectType, request.SubjectId, cancellationToken);

        return new ComplianceProfileResponse(cdd, decision, restrictions);
    }
}

// 2. Get Risk Assessment
public sealed record GetRiskAssessmentQuery(
    RiskSubjectType SubjectType,
    string SubjectId,
    Guid? OrganizationId = null) : IRequest<RiskAssessmentResult?>;

public sealed class GetRiskAssessmentQueryHandler : IRequestHandler<GetRiskAssessmentQuery, RiskAssessmentResult?>
{
    private readonly IApplicationDbContext _dbContext;

    public GetRiskAssessmentQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RiskAssessmentResult?> Handle(GetRiskAssessmentQuery request, CancellationToken cancellationToken)
    {
        var assessment = await _dbContext.RiskAssessments
            .Where(a => a.SubjectType == request.SubjectType && a.SubjectId == request.SubjectId && a.IsCurrent)
            .OrderByDescending(a => a.EvaluatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (assessment == null)
            return null;

        var factors = await _dbContext.RiskFactorResults
            .Where(f => f.RiskAssessmentId == assessment.Id)
            .ToListAsync(cancellationToken);

        var factorDtos = factors.Select(f => new RiskFactorDto(
            f.RuleId,
            f.RuleName,
            f.RiskRating,
            f.Reason,
            f.EvidenceReference,
            f.Severity)).ToList();

        return new RiskAssessmentResult(
            assessment.Id,
            assessment.SubjectType,
            assessment.SubjectId,
            assessment.OrganizationId,
            assessment.RiskRating,
            assessment.CddLevel,
            assessment.EddRequired,
            factorDtos.Any(f => f.RiskRating == RiskRating.High),
            assessment.RulesetVersion,
            assessment.EvaluatedAtUtc,
            assessment.ExpiresAtUtc,
            assessment.Summary,
            factorDtos);
    }
}

// 3. Get Risk History
public sealed record GetRiskHistoryQuery(
    RiskSubjectType SubjectType,
    string SubjectId) : IRequest<IReadOnlyList<RiskAssessmentResult>>;

public sealed class GetRiskHistoryQueryHandler : IRequestHandler<GetRiskHistoryQuery, IReadOnlyList<RiskAssessmentResult>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetRiskHistoryQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<RiskAssessmentResult>> Handle(GetRiskHistoryQuery request, CancellationToken cancellationToken)
    {
        var assessments = await _dbContext.RiskAssessments
            .Where(a => a.SubjectType == request.SubjectType && a.SubjectId == request.SubjectId)
            .OrderByDescending(a => a.EvaluatedAtUtc)
            .ToListAsync(cancellationToken);

        var allAssessmentIds = assessments.Select(a => a.Id).ToHashSet();
        var allFactors = await _dbContext.RiskFactorResults
            .Where(f => allAssessmentIds.Contains(f.RiskAssessmentId))
            .ToListAsync(cancellationToken);

        return assessments.Select(a =>
        {
            var factors = allFactors
                .Where(f => f.RiskAssessmentId == a.Id)
                .Select(f => new RiskFactorDto(
                    f.RuleId,
                    f.RuleName,
                    f.RiskRating,
                    f.Reason,
                    f.EvidenceReference,
                    f.Severity)).ToList();

            return new RiskAssessmentResult(
                a.Id,
                a.SubjectType,
                a.SubjectId,
                a.OrganizationId,
                a.RiskRating,
                a.CddLevel,
                a.EddRequired,
                factors.Any(f => f.RiskRating == RiskRating.High),
                a.RulesetVersion,
                a.EvaluatedAtUtc,
                a.ExpiresAtUtc,
                a.Summary,
                factors);
        }).ToList();
    }
}

// 4. Get EDD Case by Id
public sealed record GetEddCaseByIdQuery(Guid EddCaseId) : IRequest<EddCaseDto?>;

public sealed class GetEddCaseByIdQueryHandler : IRequestHandler<GetEddCaseByIdQuery, EddCaseDto?>
{
    private readonly IApplicationDbContext _dbContext;

    public GetEddCaseByIdQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EddCaseDto?> Handle(GetEddCaseByIdQuery request, CancellationToken cancellationToken)
    {
        var edd = await _dbContext.EddCases
            .FirstOrDefaultAsync(e => e.Id == request.EddCaseId, cancellationToken);

        if (edd == null)
            return null;

        return new EddCaseDto(
            edd.Id,
            edd.CaseNumber,
            edd.SubjectType,
            edd.SubjectId,
            edd.OrganizationId,
            edd.RiskAssessmentId,
            edd.Status,
            edd.TriggerReason,
            edd.RequiredInformation,
            edd.SubmittedInformation,
            edd.AssignedReviewerId,
            edd.ReviewedByUserId,
            edd.SeniorManagementApprovalRequired,
            edd.SeniorManagementApproverId,
            edd.Decision,
            edd.DecisionReason,
            edd.CreatedAtUtc,
            edd.UpdatedAtUtc,
            edd.CompletedAtUtc);
    }
}

// 5. Get EDD Cases (List)
public sealed record GetEddCasesQuery(
    EddStatus? Status = null,
    RiskSubjectType? SubjectType = null,
    Guid? OrganizationId = null) : IRequest<IReadOnlyList<EddCaseDto>>;

public sealed class GetEddCasesQueryHandler : IRequestHandler<GetEddCasesQuery, IReadOnlyList<EddCaseDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetEddCasesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<EddCaseDto>> Handle(GetEddCasesQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.EddCases.AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(e => e.Status == request.Status.Value);

        if (request.SubjectType.HasValue)
            query = query.Where(e => e.SubjectType == request.SubjectType.Value);

        if (request.OrganizationId.HasValue)
            query = query.Where(e => e.OrganizationId == request.OrganizationId.Value);

        var list = await query.OrderByDescending(e => e.CreatedAtUtc).ToListAsync(cancellationToken);

        return list.Select(edd => new EddCaseDto(
            edd.Id,
            edd.CaseNumber,
            edd.SubjectType,
            edd.SubjectId,
            edd.OrganizationId,
            edd.RiskAssessmentId,
            edd.Status,
            edd.TriggerReason,
            edd.RequiredInformation,
            edd.SubmittedInformation,
            edd.AssignedReviewerId,
            edd.ReviewedByUserId,
            edd.SeniorManagementApprovalRequired,
            edd.SeniorManagementApproverId,
            edd.Decision,
            edd.DecisionReason,
            edd.CreatedAtUtc,
            edd.UpdatedAtUtc,
            edd.CompletedAtUtc)).ToList();
    }
}

// 6. Get Compliance Restrictions Query
public sealed record GetComplianceRestrictionsQuery(
    RiskSubjectType SubjectType,
    string SubjectId) : IRequest<IReadOnlyList<ComplianceRestrictionDto>>;

public sealed class GetComplianceRestrictionsQueryHandler : IRequestHandler<GetComplianceRestrictionsQuery, IReadOnlyList<ComplianceRestrictionDto>>
{
    private readonly IComplianceRestrictionService _restrictionService;

    public GetComplianceRestrictionsQueryHandler(IComplianceRestrictionService restrictionService)
    {
        _restrictionService = restrictionService;
    }

    public async Task<IReadOnlyList<ComplianceRestrictionDto>> Handle(GetComplianceRestrictionsQuery request, CancellationToken cancellationToken)
    {
        return await _restrictionService.GetActiveRestrictionsAsync(request.SubjectType, request.SubjectId, cancellationToken);
    }
}

// 7. Check Transaction Eligibility Query
public sealed record CheckTransactionEligibilityQuery(
    string UserId,
    Guid? OrganizationId,
    ComplianceOperationType OperationType,
    decimal Amount,
    Currency Currency) : IRequest<TransactionEligibilityResult>;

public sealed class CheckTransactionEligibilityQueryHandler : IRequestHandler<CheckTransactionEligibilityQuery, TransactionEligibilityResult>
{
    private readonly IComplianceEligibilityService _eligibilityService;

    public CheckTransactionEligibilityQueryHandler(IComplianceEligibilityService eligibilityService)
    {
        _eligibilityService = eligibilityService;
    }

    public async Task<TransactionEligibilityResult> Handle(CheckTransactionEligibilityQuery request, CancellationToken cancellationToken)
    {
        return await _eligibilityService.EvaluateEligibilityAsync(
            request.UserId,
            request.OrganizationId,
            request.OperationType,
            request.Amount,
            request.Currency,
            cancellationToken);
    }
}
