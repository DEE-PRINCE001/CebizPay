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
/// Service calculating authoritative compliance decisions and administering tightly permissioned manual overrides.
/// </summary>
public sealed class ComplianceDecisionService : IComplianceDecisionService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IOutboxService _outboxService;

    public ComplianceDecisionService(
        IApplicationDbContext dbContext,
        IOutboxService outboxService)
    {
        _dbContext = dbContext;
        _outboxService = outboxService;
    }

    public async Task<ComplianceDecisionDto> EvaluateDecisionAsync(
        RiskSubjectType subjectType,
        string subjectId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("SubjectId is required.", nameof(subjectId));

        var cdd = await _dbContext.CddProfiles
            .FirstOrDefaultAsync(c => c.SubjectType == subjectType && c.SubjectId == subjectId, cancellationToken);

        var assessment = await _dbContext.RiskAssessments
            .Where(a => a.SubjectType == subjectType && a.SubjectId == subjectId && a.IsCurrent)
            .OrderByDescending(a => a.EvaluatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var eddCase = await _dbContext.EddCases
            .Where(e => e.SubjectType == subjectType && e.SubjectId == subjectId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var existingDecision = await _dbContext.ComplianceDecisions
            .Where(d => d.SubjectType == subjectType && d.SubjectId == subjectId && d.IsActive)
            .OrderByDescending(d => d.EffectiveFromUtc)
            .FirstOrDefaultAsync(cancellationToken);

        // Respect non-expired manual override unless prohibited sanctions match is present
        if (existingDecision is { IsManualOverride: true })
        {
            if (assessment?.RiskRating != RiskRating.Prohibited &&
                (!existingDecision.ExpiresAtUtc.HasValue || existingDecision.ExpiresAtUtc.Value > DateTime.UtcNow))
            {
                return MapToDto(existingDecision);
            }
        }

        var riskRating = assessment?.RiskRating ?? RiskRating.Medium;
        var cddLevel = cdd?.CddLevel ?? CddLevel.Standard;
        var rulesetVersion = assessment?.RulesetVersion ?? RiskEngine.CurrentRulesetVersion;

        ComplianceDecisionType decisionType;
        string reasons;

        if (riskRating == RiskRating.Prohibited || cdd?.Status == CddStatus.Suspended)
        {
            decisionType = ComplianceDecisionType.Suspended;
            reasons = "Account suspended due to confirmed sanctions watchlist match or severe regulatory compliance violation.";
        }
        else if (eddCase is { Status: EddStatus.Approved })
        {
            decisionType = ComplianceDecisionType.Approved;
            reasons = $"Approved following successful completion of Enhanced Due Diligence (EDD Case {eddCase.CaseNumber}).";
        }
        else if (eddCase is { Status: EddStatus.Rejected })
        {
            decisionType = ComplianceDecisionType.Rejected;
            reasons = $"Rejected following Enhanced Due Diligence review (EDD Case {eddCase.CaseNumber}). Reason: {eddCase.DecisionReason}";
        }
        else if (cdd?.Status == CddStatus.EnhancedRequired || eddCase != null)
        {
            decisionType = ComplianceDecisionType.EddRequired;
            reasons = "Enhanced Due Diligence (EDD) documentation and review required before unrestricted operation.";
        }
        else if (cdd?.Status == CddStatus.ReviewRequired || riskRating == RiskRating.High)
        {
            decisionType = ComplianceDecisionType.ReviewRequired;
            reasons = assessment?.Summary ?? "Manual compliance officer review required due to risk flags.";
        }
        else if (cdd?.Status == CddStatus.Completed)
        {
            decisionType = ComplianceDecisionType.Approved;
            reasons = "Customer Due Diligence (CDD) verification requirements satisfied.";
        }
        else
        {
            decisionType = ComplianceDecisionType.ReviewRequired;
            reasons = "Customer Due Diligence verification is pending or incomplete.";
        }

        if (existingDecision != null && existingDecision.Decision == decisionType && !existingDecision.IsManualOverride)
        {
            return MapToDto(existingDecision);
        }

        if (existingDecision != null)
        {
            existingDecision.Deactivate();
        }

        var decision = ComplianceDecision.Create(
            subjectType,
            subjectId,
            decisionType,
            riskRating,
            cddLevel,
            reasons,
            rulesetVersion,
            "System",
            eddCase?.Status,
            organizationId);

        _dbContext.ComplianceDecisions.Add(decision);

        _outboxService.Write(new ComplianceDecisionChangedDomainEvent(
            decision.Id,
            decision.SubjectType,
            decision.SubjectId,
            decision.Decision,
            decision.RiskRating,
            decision.IsManualOverride,
            decision.OrganizationId,
            decision.EffectiveFromUtc));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(decision);
    }

    public async Task<ComplianceDecisionDto> ApplyManualOverrideAsync(
        RiskSubjectType subjectType,
        string subjectId,
        ComplianceDecisionType newDecision,
        string reason,
        string adminUserId,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
            throw new ArgumentException("SubjectId is required.", nameof(subjectId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));
        if (string.IsNullOrWhiteSpace(adminUserId))
            throw new ArgumentException("AdminUserId is required.", nameof(adminUserId));

        var assessment = await _dbContext.RiskAssessments
            .Where(a => a.SubjectType == subjectType && a.SubjectId == subjectId && a.IsCurrent)
            .OrderByDescending(a => a.EvaluatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        // Non-negotiable regulatory control: Prohibited sanctions match cannot be overridden to Approved
        if (assessment?.RiskRating == RiskRating.Prohibited && newDecision == ComplianceDecisionType.Approved)
        {
            throw new InvalidOperationException("Non-negotiable regulatory safeguard: Accounts with confirmed sanctions watchlist matches cannot be manually approved.");
        }

        var cdd = await _dbContext.CddProfiles
            .FirstOrDefaultAsync(c => c.SubjectType == subjectType && c.SubjectId == subjectId, cancellationToken);

        var existingDecisions = await _dbContext.ComplianceDecisions
            .Where(d => d.SubjectType == subjectType && d.SubjectId == subjectId && d.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var d in existingDecisions)
        {
            d.Deactivate();
        }

        var riskRating = assessment?.RiskRating ?? RiskRating.Medium;
        var cddLevel = cdd?.CddLevel ?? CddLevel.Standard;
        var rulesetVersion = assessment?.RulesetVersion ?? RiskEngine.CurrentRulesetVersion;

        var overrideDecision = ComplianceDecision.CreateManualOverride(
            subjectType,
            subjectId,
            newDecision,
            riskRating,
            cddLevel,
            reason,
            adminUserId,
            rulesetVersion,
            organizationId: organizationId);

        _dbContext.ComplianceDecisions.Add(overrideDecision);

        _outboxService.Write(new ComplianceDecisionChangedDomainEvent(
            overrideDecision.Id,
            overrideDecision.SubjectType,
            overrideDecision.SubjectId,
            overrideDecision.Decision,
            overrideDecision.RiskRating,
            true,
            overrideDecision.OrganizationId,
            overrideDecision.EffectiveFromUtc));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(overrideDecision);
    }

    private static ComplianceDecisionDto MapToDto(ComplianceDecision decision) =>
        new(
            decision.Id,
            decision.SubjectType,
            decision.SubjectId,
            decision.OrganizationId,
            decision.Decision,
            decision.RiskRating,
            decision.CddLevel,
            decision.EddStatus,
            decision.DecisionReasons,
            decision.RulesetVersion,
            decision.DecidedBy,
            decision.IsManualOverride,
            decision.OverrideReason,
            decision.EffectiveFromUtc,
            decision.ExpiresAtUtc,
            decision.IsActive);
}
