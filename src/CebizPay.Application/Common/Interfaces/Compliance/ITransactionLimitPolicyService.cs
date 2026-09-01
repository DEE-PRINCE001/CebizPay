#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Service managing versioned transaction limit policies, statutory regulatory bounds,
/// product policies, provider constraints, and effective limit calculations.
/// </summary>
public interface ITransactionLimitPolicyService
{
    /// <summary>
    /// Returns the currently active transaction limit policy.
    /// </summary>
    TransactionLimitPolicy GetActivePolicy();

    /// <summary>
    /// Returns a specific version of a transaction limit policy, preserving immutability for historical assessments.
    /// </summary>
    TransactionLimitPolicy GetPolicyByVersion(string version);

    /// <summary>
    /// Calculates the effective single and daily transaction limits across regulatory, product,
    /// provider, and account-specific layers.
    /// </summary>
    EffectiveTransactionLimit CalculateEffectiveLimit(
        RiskSubjectType subjectType,
        int? individualTier,
        ComplianceOperationType operationType,
        decimal? customerSingleCap = null,
        string? provider = null);

    /// <summary>
    /// Returns the volume threshold that triggers an Enhanced Due Diligence (EDD) case.
    /// </summary>
    decimal GetEddVolumeThreshold(RiskSubjectType subjectType, ComplianceOperationType operationType);

    /// <summary>
    /// Registers or updates a versioned limit policy.
    /// </summary>
    void RegisterPolicy(TransactionLimitPolicy policy);
}
