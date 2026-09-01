#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Compliance.Events;
using CebizPay.Domain.Finance.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Compliance.Services;

/// <summary>
/// Centralized Risk Engine executing deterministic, explainable rulesets and persisting immutable assessment history.
/// </summary>
public sealed class RiskEngine : IRiskEngine
{
    public const string CurrentRulesetVersion = "2026.1";

    private readonly IApplicationDbContext _dbContext;
    private readonly IOutboxService _outboxService;
    private readonly IEnumerable<IRiskRule> _rules;
    private readonly RiskMetrics _metrics;
    private readonly ILogger<RiskEngine> _logger;

    public RiskEngine(
        IApplicationDbContext dbContext,
        IOutboxService outboxService,
        IEnumerable<IRiskRule> rules,
        RiskMetrics metrics,
        ILogger<RiskEngine> logger)
    {
        _dbContext = dbContext;
        _outboxService = outboxService;
        _rules = rules.OrderBy(r => r.Priority).ToList();
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<RiskAssessmentResult> EvaluateIndividualRiskAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));

        var profile = await _dbContext.IndividualProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        var kycDocs = await _dbContext.KycDocuments
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .ToListAsync(cancellationToken);

        var evidences = await _dbContext.VerificationEvidences
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.VerifiedAtUtc)
            .ToListAsync(cancellationToken);

        var context = new RiskEvaluationContext
        {
            SubjectType = RiskSubjectType.Individual,
            SubjectId = userId,
            IndividualProfile = profile,
            KycDocuments = kycDocs,
            VerificationEvidences = evidences
        };

        return await ExecuteRiskEvaluationAsync(context, cancellationToken);
    }

    public async Task<RiskAssessmentResult> EvaluateOrganizationRiskAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));

        var org = await _dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

        var kyb = await _dbContext.KybDetails
            .AsNoTracking()
            .Where(k => k.OrganizationId == organizationId)
            .OrderByDescending(k => k.SubmittedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var evidences = await _dbContext.VerificationEvidences
            .AsNoTracking()
            .Where(e => e.OrganizationId == organizationId)
            .OrderByDescending(e => e.VerifiedAtUtc)
            .ToListAsync(cancellationToken);

        var context = new RiskEvaluationContext
        {
            SubjectType = RiskSubjectType.Organization,
            SubjectId = organizationId.ToString(),
            OrganizationId = organizationId,
            Organization = org,
            KybDetail = kyb,
            VerificationEvidences = evidences
        };

        return await ExecuteRiskEvaluationAsync(context, cancellationToken);
    }

    public async Task<RiskAssessmentResult> EvaluateTransactionRiskAsync(
        string userId,
        Guid? organizationId,
        ComplianceOperationType operationType,
        decimal amount,
        Currency currency,
        CancellationToken cancellationToken = default)
    {
        var evidences = await _dbContext.VerificationEvidences
            .AsNoTracking()
            .Where(e => e.UserId == userId || (organizationId.HasValue && e.OrganizationId == organizationId))
            .OrderByDescending(e => e.VerifiedAtUtc)
            .ToListAsync(cancellationToken);

        var context = new RiskEvaluationContext
        {
            SubjectType = RiskSubjectType.Transaction,
            SubjectId = $"{userId}-{operationType}-{DateTime.UtcNow.Ticks}",
            OrganizationId = organizationId,
            VerificationEvidences = evidences,
            OperationType = operationType,
            TransactionAmount = amount,
            Currency = currency
        };

        return await ExecuteRiskEvaluationAsync(context, cancellationToken, isEphemeralTransaction: true);
    }

    private async Task<RiskAssessmentResult> ExecuteRiskEvaluationAsync(
        RiskEvaluationContext context,
        CancellationToken cancellationToken,
        bool isEphemeralTransaction = false)
    {
        var applicableRules = _rules.Where(r => r.CanEvaluate(context.SubjectType)).ToList();
        var ruleResults = new List<RiskRuleEvaluationResult>();

        foreach (var rule in applicableRules)
        {
            var res = await rule.EvaluateAsync(context, cancellationToken);
            ruleResults.Add(res);
        }

        // Determine aggregated risk rating
        RiskRating overallRating = RiskRating.Low;
        if (ruleResults.Any(r => r.RiskRating == RiskRating.Prohibited))
        {
            overallRating = RiskRating.Prohibited;
        }
        else if (ruleResults.Any(r => r.RiskRating == RiskRating.High))
        {
            overallRating = RiskRating.High;
        }
        else if (ruleResults.Any(r => r.RiskRating == RiskRating.Medium))
        {
            overallRating = RiskRating.Medium;
        }

        var eddRequired = ruleResults.Any(r => r.TriggersEdd) || overallRating == RiskRating.High;
        var seniorMgmtRequired = ruleResults.Any(r => r.RequiresSeniorManagement);

        CddLevel cddLevel;
        if (overallRating == RiskRating.Prohibited || eddRequired)
        {
            cddLevel = CddLevel.Enhanced;
        }
        else if (context.SubjectType == RiskSubjectType.Organization || overallRating == RiskRating.Medium)
        {
            cddLevel = CddLevel.Standard;
        }
        else
        {
            cddLevel = CddLevel.Basic;
        }

        var summaryReasons = ruleResults
            .Where(r => r.RiskRating != RiskRating.Low || !string.IsNullOrWhiteSpace(r.Reason))
            .Select(r => $"{r.RuleId}: {r.Reason}")
            .ToList();

        var summary = summaryReasons.Count > 0
            ? string.Join("; ", summaryReasons)
            : "All standard risk checks passed with low risk indicators.";

        // Handle persistence for non-ephemeral evaluations
        var assessment = RiskAssessment.Create(
            context.SubjectType,
            context.SubjectId,
            overallRating,
            cddLevel,
            eddRequired,
            CurrentRulesetVersion,
            summary,
            context.OrganizationId);

        foreach (var res in ruleResults)
        {
            var factor = RiskFactorResult.Create(
                assessment.Id,
                res.RuleId,
                res.RuleName,
                res.RiskRating,
                res.Reason,
                res.EvidenceReference,
                res.Severity);
            assessment.AddRiskFactor(factor);
        }

        if (!isEphemeralTransaction)
        {
            // Supersede previous current assessments
            var previousAssessments = await _dbContext.RiskAssessments
                .Where(a => a.SubjectType == context.SubjectType && a.SubjectId == context.SubjectId && a.IsCurrent)
                .ToListAsync(cancellationToken);

            RiskRating? previousRating = null;
            Guid? previousId = null;

            foreach (var prev in previousAssessments)
            {
                prev.MarkSuperseded();
                previousRating = prev.RiskRating;
                previousId = prev.Id;
            }

            _dbContext.RiskAssessments.Add(assessment);
            foreach (var factor in assessment.RiskFactors)
            {
                _dbContext.RiskFactorResults.Add(factor);
            }

            // Write domain events
            _outboxService.Write(new RiskAssessmentCompletedDomainEvent(
                assessment.Id,
                assessment.SubjectType,
                assessment.SubjectId,
                assessment.RiskRating,
                assessment.CddLevel,
                assessment.EddRequired,
                assessment.RulesetVersion,
                assessment.OrganizationId,
                assessment.EvaluatedAtUtc));

            if (previousRating.HasValue && previousRating.Value != overallRating && previousId.HasValue)
            {
                _outboxService.Write(new RiskAssessmentChangedDomainEvent(
                    previousId.Value,
                    assessment.Id,
                    assessment.SubjectType,
                    assessment.SubjectId,
                    previousRating.Value,
                    overallRating,
                    assessment.OrganizationId,
                    assessment.EvaluatedAtUtc));

                _metrics.RecordRiskChanged(context.SubjectType, previousRating.Value, overallRating);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _metrics.RecordRiskAssessment(context.SubjectType, overallRating);
        }

        var factorDtos = assessment.RiskFactors.Select(f => new RiskFactorDto(
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
            seniorMgmtRequired,
            assessment.RulesetVersion,
            assessment.EvaluatedAtUtc,
            assessment.ExpiresAtUtc,
            assessment.Summary,
            factorDtos);
    }
}
