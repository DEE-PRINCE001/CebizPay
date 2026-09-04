using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Savings;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Savings.Entities;
using CebizPay.Domain.Savings.Enums;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Savings;

/// <summary>
/// Implementation of ISavingsInterestPolicyService managing versioned Super Admin interest policies.
/// </summary>
public class SavingsInterestPolicyService : ISavingsInterestPolicyService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly CebizPay.Application.Common.Interfaces.Security.ICurrentUserService? _currentUserService;

    /// <summary>
    /// Initializes a new instance of SavingsInterestPolicyService.
    /// </summary>
    public SavingsInterestPolicyService(
        IApplicationDbContext dbContext,
        CebizPay.Application.Common.Interfaces.Security.ICurrentUserService? currentUserService = null)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    /// <inheritdoc/>
    public async Task<SavingsInterestPolicyDto?> GetActivePolicyAsync(SavingsPlanType planType, CancellationToken cancellationToken = default)
    {
        var policy = await _dbContext.SavingsInterestPolicies
            .Where(p => p.PlanType == planType && p.IsActive)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(cancellationToken);

        return policy == null ? null : MapToDto(policy);
    }

    /// <inheritdoc/>
    public async Task<SavingsInterestPolicyDto> CreateAndActivatePolicyAsync(CreateSavingsInterestPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var actorId = _currentUserService?.UserId ?? "SYSTEM";
        if (!string.IsNullOrWhiteSpace(_currentUserService?.UserId))
        {
            var admin = await _dbContext.AdminProfiles
                .FirstOrDefaultAsync(a => a.UserId == _currentUserService.UserId && !a.IsDeleted && a.IsActive, cancellationToken);
            if (admin == null || admin.Role != Domain.Enums.AdminRoleType.SuperAdmin)
            {
                throw new UnauthorizedAccessException("Only Super Admins can configure platform savings interest policies.");
            }
        }

        // Find existing active policies for this plan type and deactivate them
        var activePolicies = await _dbContext.SavingsInterestPolicies
            .Where(p => p.PlanType == request.PlanType && p.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var activePolicy in activePolicies)
        {
            activePolicy.Deactivate();
            var deactivateAudit = AuditLog.Create(
                actorId: actorId,
                action: AuditActions.SavingsInterestPolicyDeactivated,
                resourceType: AuditResourceTypes.SavingsInterestPolicy,
                resourceId: activePolicy.Id.ToString(),
                afterJson: $"{{\"reason\":\"Deactivated Savings Interest Policy v{activePolicy.Version} for {activePolicy.PlanType}\"}}");
            _dbContext.AuditLogs.Add(deactivateAudit);
        }

        // Determine next version
        var maxVersion = await _dbContext.SavingsInterestPolicies
            .Where(p => p.PlanType == request.PlanType)
            .MaxAsync(p => (int?)p.Version, cancellationToken) ?? 0;

        var nextVersion = maxVersion + 1;

        var newPolicy = SavingsInterestPolicy.Create(
            request.PlanType,
            request.Mode,
            request.AnnualRate,
            nextVersion,
            DateTime.UtcNow);

        _dbContext.SavingsInterestPolicies.Add(newPolicy);

        var audit = AuditLog.Create(
            actorId: actorId,
            action: AuditActions.SavingsInterestPolicyCreated,
            resourceType: AuditResourceTypes.SavingsInterestPolicy,
            resourceId: newPolicy.Id.ToString(),
            afterJson: $"{{\"version\":{newPolicy.Version},\"mode\":\"{newPolicy.Mode}\",\"annualRate\":{newPolicy.AnnualRate}}}");
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(newPolicy);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SavingsInterestPolicyDto>> GetAllPoliciesAsync(CancellationToken cancellationToken = default)
    {
        var policies = await _dbContext.SavingsInterestPolicies
            .OrderByDescending(p => p.EffectiveFromUtc)
            .ThenByDescending(p => p.Version)
            .ToListAsync(cancellationToken);

        return policies.Select(MapToDto).ToList();
    }

    private static SavingsInterestPolicyDto MapToDto(SavingsInterestPolicy policy) =>
        new(
            policy.Id,
            policy.PlanType,
            policy.Mode,
            policy.AnnualRate,
            policy.Version,
            policy.EffectiveFromUtc,
            policy.DeactivatedAtUtc,
            policy.IsActive);
}
