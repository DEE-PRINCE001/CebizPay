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
/// Service managing granular operational and volume compliance restrictions.
/// </summary>
public sealed class ComplianceRestrictionService : IComplianceRestrictionService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IOutboxService _outboxService;
    private readonly RiskMetrics _metrics;

    public ComplianceRestrictionService(
        IApplicationDbContext dbContext,
        IOutboxService outboxService,
        RiskMetrics metrics)
    {
        _dbContext = dbContext;
        _outboxService = outboxService;
        _metrics = metrics;
    }

    public async Task<ComplianceRestrictionDto> PlaceRestrictionAsync(
        RiskSubjectType subjectType,
        string subjectId,
        ComplianceRestrictionType restrictionType,
        string reason,
        string placedBy,
        decimal? dailyCapAmount = null,
        decimal? singleCapAmount = null,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default)
    {
        var restriction = ComplianceRestriction.Create(
            subjectType,
            subjectId,
            restrictionType,
            reason,
            placedBy,
            dailyCapAmount,
            singleCapAmount,
            organizationId);

        _dbContext.ComplianceRestrictions.Add(restriction);

        _outboxService.Write(new ComplianceRestrictedDomainEvent(
            restriction.Id,
            restriction.SubjectType,
            restriction.SubjectId,
            restriction.RestrictionType,
            restriction.Reason,
            restriction.OrganizationId,
            restriction.PlacedAtUtc));

        _metrics.RecordRestrictionPlaced(subjectType, restrictionType);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(restriction);
    }

    public async Task<ComplianceRestrictionDto> ReleaseRestrictionAsync(
        Guid restrictionId,
        string releaseReason,
        string releasedBy,
        CancellationToken cancellationToken = default)
    {
        var restriction = await _dbContext.ComplianceRestrictions
            .FirstOrDefaultAsync(r => r.Id == restrictionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Compliance restriction {restrictionId} not found.");

        restriction.Release(releaseReason, releasedBy);

        _outboxService.Write(new ComplianceRestrictionReleasedDomainEvent(
            restriction.Id,
            restriction.SubjectType,
            restriction.SubjectId,
            releasedBy,
            releaseReason,
            restriction.OrganizationId,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(restriction);
    }

    public async Task<IReadOnlyList<ComplianceRestrictionDto>> GetActiveRestrictionsAsync(
        RiskSubjectType subjectType,
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        var list = await _dbContext.ComplianceRestrictions
            .AsNoTracking()
            .Where(r => r.SubjectType == subjectType && r.SubjectId == subjectId && r.IsActive)
            .OrderByDescending(r => r.PlacedAtUtc)
            .ToListAsync(cancellationToken);

        return list.Select(MapToDto).ToList();
    }

    private static ComplianceRestrictionDto MapToDto(ComplianceRestriction restriction) =>
        new(
            restriction.Id,
            restriction.SubjectType,
            restriction.SubjectId,
            restriction.OrganizationId,
            restriction.RestrictionType,
            restriction.Reason,
            restriction.DailyCapAmount,
            restriction.SingleCapAmount,
            restriction.PlacedBy,
            restriction.PlacedAtUtc,
            restriction.IsActive,
            restriction.ReleasedBy,
            restriction.ReleasedAtUtc,
            restriction.ReleaseReason);
}
