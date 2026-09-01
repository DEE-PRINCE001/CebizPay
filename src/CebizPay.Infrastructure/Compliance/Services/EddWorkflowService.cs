#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Compliance.Events;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Compliance.Services;

/// <summary>
/// Service managing the lifecycle of Enhanced Due Diligence (EDD) cases.
/// </summary>
public sealed class EddWorkflowService : IEddWorkflowService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IOutboxService _outboxService;
    private readonly RiskMetrics _metrics;

    public EddWorkflowService(
        IApplicationDbContext dbContext,
        IOutboxService outboxService,
        RiskMetrics metrics)
    {
        _dbContext = dbContext;
        _outboxService = outboxService;
        _metrics = metrics;
    }

    public async Task<EddCaseDto> OpenEddCaseAsync(
        RiskSubjectType subjectType,
        string subjectId,
        Guid riskAssessmentId,
        string triggerReason,
        string requiredInformation,
        bool seniorMgmtApprovalRequired = false,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        var activeCase = await _dbContext.EddCases
            .FirstOrDefaultAsync(e => e.SubjectType == subjectType && e.SubjectId == subjectId &&
                                     (e.Status != EddStatus.Approved && e.Status != EddStatus.Rejected), cancellationToken);

        if (activeCase != null)
            return MapToDto(activeCase);

        var eddCase = EddCase.Create(
            subjectType,
            subjectId,
            riskAssessmentId,
            triggerReason,
            requiredInformation,
            seniorMgmtApprovalRequired,
            organizationId);

        _dbContext.EddCases.Add(eddCase);

        _outboxService.Write(new EddCaseOpenedDomainEvent(
            eddCase.Id,
            eddCase.CaseNumber,
            eddCase.SubjectType,
            eddCase.SubjectId,
            eddCase.OrganizationId,
            eddCase.CreatedAtUtc));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(eddCase);
    }

    public async Task<EddCaseDto> RequestEddInformationAsync(
        Guid eddCaseId,
        string additionalRequirement,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        var edd = await _dbContext.EddCases.FirstOrDefaultAsync(e => e.Id == eddCaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"EDD case {eddCaseId} not found.");

        edd.RequestInformation(additionalRequirement, adminUserId);

        _outboxService.Write(new EddInformationRequestedDomainEvent(
            edd.Id,
            edd.CaseNumber,
            adminUserId,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(edd);
    }

    public async Task<EddCaseDto> SubmitEddInformationAsync(
        Guid eddCaseId,
        string submittedInformation,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var edd = await _dbContext.EddCases.FirstOrDefaultAsync(e => e.Id == eddCaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"EDD case {eddCaseId} not found.");

        edd.SubmitInformation(submittedInformation);

        _outboxService.Write(new EddInformationSubmittedDomainEvent(
            edd.Id,
            edd.CaseNumber,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(edd);
    }

    public async Task<EddCaseDto> AssignReviewerAsync(
        Guid eddCaseId,
        string reviewerAdminUserId,
        CancellationToken cancellationToken = default)
    {
        var edd = await _dbContext.EddCases.FirstOrDefaultAsync(e => e.Id == eddCaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"EDD case {eddCaseId} not found.");

        edd.AssignReviewer(reviewerAdminUserId);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(edd);
    }

    public async Task<EddCaseDto> ApproveEddCaseAsync(
        Guid eddCaseId,
        string reason,
        string adminUserId,
        bool isSeniorManagement = false,
        CancellationToken cancellationToken = default)
    {
        var edd = await _dbContext.EddCases.FirstOrDefaultAsync(e => e.Id == eddCaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"EDD case {eddCaseId} not found.");

        edd.Approve(reason, adminUserId, isSeniorManagement);

        // Advance CDD profile
        var cdd = await _dbContext.CddProfiles
            .FirstOrDefaultAsync(c => c.SubjectType == edd.SubjectType && c.SubjectId == edd.SubjectId, cancellationToken);
        if (cdd != null)
        {
            cdd.MarkCompleted($"EDD case {edd.CaseNumber} approved by {adminUserId}.");
        }

        _outboxService.Write(new EddCaseApprovedDomainEvent(
            edd.Id,
            edd.CaseNumber,
            adminUserId,
            isSeniorManagement,
            DateTime.UtcNow));

        _metrics.RecordEddCompleted(edd.SubjectType, ComplianceDecisionType.Approved);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(edd);
    }

    public async Task<EddCaseDto> RejectEddCaseAsync(
        Guid eddCaseId,
        string reason,
        string adminUserId,
        CancellationToken cancellationToken = default)
    {
        var edd = await _dbContext.EddCases.FirstOrDefaultAsync(e => e.Id == eddCaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"EDD case {eddCaseId} not found.");

        edd.Reject(reason, adminUserId);

        var cdd = await _dbContext.CddProfiles
            .FirstOrDefaultAsync(c => c.SubjectType == edd.SubjectType && c.SubjectId == edd.SubjectId, cancellationToken);
        if (cdd != null)
        {
            cdd.MarkReviewRequired($"EDD case {edd.CaseNumber} rejected by {adminUserId}. Reason: {reason}");
        }

        _outboxService.Write(new EddCaseRejectedDomainEvent(
            edd.Id,
            edd.CaseNumber,
            adminUserId,
            reason,
            DateTime.UtcNow));

        _metrics.RecordEddCompleted(edd.SubjectType, ComplianceDecisionType.Rejected);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(edd);
    }

    private static EddCaseDto MapToDto(EddCase edd) =>
        new(
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
