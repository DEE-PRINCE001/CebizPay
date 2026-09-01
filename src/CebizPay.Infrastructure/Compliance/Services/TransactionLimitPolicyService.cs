#pragma warning disable CS1591
using System.Collections.Concurrent;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Infrastructure.Compliance.Services;

/// <summary>
/// Thread-safe implementation of versioned transaction limit policies, statutory regulatory bounds,
/// product policies, provider constraints, and effective limit calculations.
/// </summary>
public sealed class TransactionLimitPolicyService : ITransactionLimitPolicyService
{
    private readonly ConcurrentDictionary<string, TransactionLimitPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);
    private volatile string _activeVersion;

    public TransactionLimitPolicyService()
    {
        var defaultPolicy = new TransactionLimitPolicy
        {
            PolicyId = "POL-NGN-DEFAULT-2026",
            Version = TransactionLimitPolicy.DefaultVersion,
            EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        _policies[defaultPolicy.Version] = defaultPolicy;
        _activeVersion = defaultPolicy.Version;
    }

    public TransactionLimitPolicy GetActivePolicy()
    {
        if (_policies.TryGetValue(_activeVersion, out var policy))
        {
            return policy;
        }

        return new TransactionLimitPolicy();
    }

    public TransactionLimitPolicy GetPolicyByVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return GetActivePolicy();

        if (_policies.TryGetValue(version.Trim(), out var policy))
            return policy;

        return GetActivePolicy();
    }

    public EffectiveTransactionLimit CalculateEffectiveLimit(
        RiskSubjectType subjectType,
        int? individualTier,
        ComplianceOperationType operationType,
        decimal? customerSingleCap = null,
        string? provider = null)
    {
        var activePolicy = GetActivePolicy();
        return activePolicy.CalculateEffectiveLimit(subjectType, individualTier, operationType, customerSingleCap, provider);
    }

    public decimal GetEddVolumeThreshold(RiskSubjectType subjectType, ComplianceOperationType operationType)
    {
        var activePolicy = GetActivePolicy();
        return subjectType == RiskSubjectType.Organization
            ? activePolicy.CorporateEddVolumeThreshold
            : activePolicy.IndividualEddVolumeThreshold;
    }

    public void RegisterPolicy(TransactionLimitPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (string.IsNullOrWhiteSpace(policy.Version))
            throw new ArgumentException("Policy Version is required.", nameof(policy));

        _policies[policy.Version.Trim()] = policy;
        _activeVersion = policy.Version.Trim();
    }
}
