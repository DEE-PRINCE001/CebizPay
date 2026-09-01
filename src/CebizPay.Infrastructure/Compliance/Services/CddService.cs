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
/// Service managing Customer Due Diligence (CDD) profiles, individual KYC tier computation, and regulatory compliance status.
/// </summary>
public sealed class CddService : ICddService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IOutboxService _outboxService;
    private readonly IRiskEngine _riskEngine;
    private readonly IEddWorkflowService _eddService;
    private readonly RiskMetrics _metrics;

    public CddService(
        IApplicationDbContext dbContext,
        IOutboxService outboxService,
        IRiskEngine riskEngine,
        IEddWorkflowService eddService,
        RiskMetrics metrics)
    {
        _dbContext = dbContext;
        _outboxService = outboxService;
        _riskEngine = riskEngine;
        _eddService = eddService;
        _metrics = metrics;
    }

    public async Task<CddProfileDto> GetOrCreateCddProfileAsync(
        RiskSubjectType subjectType,
        string subjectId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("SubjectId is required.", nameof(subjectId));

        var profile = await _dbContext.CddProfiles
            .FirstOrDefaultAsync(p => p.SubjectType == subjectType && p.SubjectId == subjectId, cancellationToken);

        if (profile == null)
        {
            profile = CddProfile.Create(subjectType, subjectId, organizationId);
            _dbContext.CddProfiles.Add(profile);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return MapToDto(profile);
    }

    public async Task<CddProfileDto> EvaluateCddAsync(
        RiskSubjectType subjectType,
        string subjectId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("SubjectId is required.", nameof(subjectId));

        var profile = await _dbContext.CddProfiles
            .FirstOrDefaultAsync(p => p.SubjectType == subjectType && p.SubjectId == subjectId, cancellationToken);

        if (profile == null)
        {
            profile = CddProfile.Create(subjectType, subjectId, organizationId);
            _dbContext.CddProfiles.Add(profile);
        }

        // Fetch latest risk assessment or evaluate fresh
        var assessment = await _dbContext.RiskAssessments
            .Include(a => a.RiskFactors)
            .Where(a => a.SubjectType == subjectType && a.SubjectId == subjectId && a.IsCurrent)
            .OrderByDescending(a => a.EvaluatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (assessment == null)
        {
            if (subjectType == RiskSubjectType.Individual)
            {
                await _riskEngine.EvaluateIndividualRiskAsync(subjectId, cancellationToken);
            }
            else if (subjectType == RiskSubjectType.Organization && Guid.TryParse(subjectId, out var orgId))
            {
                await _riskEngine.EvaluateOrganizationRiskAsync(orgId, cancellationToken);
            }

            assessment = await _dbContext.RiskAssessments
                .Include(a => a.RiskFactors)
                .Where(a => a.SubjectType == subjectType && a.SubjectId == subjectId && a.IsCurrent)
                .OrderByDescending(a => a.EvaluatedAtUtc)
                .FirstAsync(cancellationToken);
        }

        // Compute Tier if Individual (Tier 1, 2, 3 based on CBN Tiered KYC)
        int? tier = null;
        if (subjectType == RiskSubjectType.Individual)
        {
            var evidences = await _dbContext.VerificationEvidences
                .AsNoTracking()
                .Where(e => e.UserId == subjectId && e.ResultStatus == VerificationResultStatus.Match)
                .ToListAsync(cancellationToken);

            var hasIdMatch = evidences.Any(e => e.Capability == VerificationCapability.Identity);
            var hasDocMatch = evidences.Any(e => e.Capability == VerificationCapability.Document);
            var hasBioMatch = evidences.Any(e => e.Capability == VerificationCapability.Biometrics);

            if (hasIdMatch && hasDocMatch && hasBioMatch)
            {
                tier = 3;
            }
            else if (hasIdMatch && hasDocMatch)
            {
                tier = 2;
            }
            else if (hasIdMatch)
            {
                tier = 1;
            }
        }

        profile.UpdateFromAssessment(assessment, tier);

        if (profile.Status == CddStatus.Completed)
        {
            _outboxService.Write(new CddCompletedDomainEvent(
                profile.Id,
                profile.SubjectType,
                profile.SubjectId,
                profile.RiskRating,
                profile.CddLevel,
                profile.Tier,
                profile.OrganizationId,
                profile.CompletedAtUtc ?? DateTime.UtcNow));

            _metrics.RecordCddCompleted(profile.SubjectType, profile.CddLevel);
        }
        else if (profile.Status == CddStatus.EnhancedRequired)
        {
            _outboxService.Write(new EddRequiredDomainEvent(
                profile.SubjectType,
                profile.SubjectId,
                assessment.Id,
                assessment.Summary,
                profile.OrganizationId,
                DateTime.UtcNow));

            _metrics.RecordEddRequired(profile.SubjectType);

            // Automatically ensure an active EDD case exists
            var existingEdd = await _dbContext.EddCases
                .FirstOrDefaultAsync(e => e.SubjectType == subjectType && e.SubjectId == subjectId &&
                                         (e.Status == EddStatus.Required || e.Status == EddStatus.Initiated || e.Status == EddStatus.InformationRequested || e.Status == EddStatus.InformationSubmitted || e.Status == EddStatus.InReview), cancellationToken);

            if (existingEdd == null)
            {
                var requiresSeniorMgmt = assessment.RiskFactors.Any(f => f.RiskRating == RiskRating.High && f.RuleId.Contains("PEP"));
                await _eddService.OpenEddCaseAsync(
                    subjectType,
                    subjectId,
                    assessment.Id,
                    assessment.Summary,
                    "Provide Source of Funds, Proof of Income / Wealth, and purpose of financial relationship.",
                    requiresSeniorMgmt,
                    organizationId,
                    cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(profile);
    }

    private static CddProfileDto MapToDto(CddProfile profile) =>
        new(
            profile.Id,
            profile.SubjectType,
            profile.SubjectId,
            profile.OrganizationId,
            profile.Status,
            profile.RiskRating,
            profile.CddLevel,
            profile.Tier,
            profile.LatestRiskAssessmentId,
            profile.CompletedAtUtc,
            profile.LastEvaluatedAtUtc,
            profile.ReviewNotes);
}
