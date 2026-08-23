using CebizPay.Domain.Savings.Enums;

namespace CebizPay.Domain.Savings.Entities;

/// <summary>
/// Domain aggregate root representing an authoritative versioned interest policy configured by Super Admin.
/// Governs interest rates and accrual modes applied to savings products.
/// </summary>
public class SavingsInterestPolicy
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Applicable savings plan type (e.g. GoalBased or FixedLock).</summary>
    public SavingsPlanType PlanType { get; private set; }

    /// <summary>Interest computation mode (None = 0%, Percentage = configured annual rate).</summary>
    public GoalInterestPolicyMode Mode { get; private set; }

    /// <summary>Annual interest rate expressed as a decimal (e.g. 0.08m for 8% per annum).</summary>
    public decimal AnnualRate { get; private set; }

    /// <summary>Monotonically increasing version number for effective-dating audit trails.</summary>
    public int Version { get; private set; }

    /// <summary>Timestamp when this policy version became effective.</summary>
    public DateTime EffectiveFromUtc { get; private set; }

    /// <summary>Timestamp when this policy version was superseded / deactivated.</summary>
    public DateTime? DeactivatedAtUtc { get; private set; }

    /// <summary>Indicates whether this policy version is currently active.</summary>
    public bool IsActive { get; private set; }

    private SavingsInterestPolicy() { } // EF Core

    /// <summary>
    /// Creates a new versioned savings interest policy.
    /// </summary>
    public static SavingsInterestPolicy Create(
        SavingsPlanType planType,
        GoalInterestPolicyMode mode,
        decimal annualRate,
        int version,
        DateTime effectiveFromUtc)
    {
        if (version <= 0)
            throw new ArgumentException("Policy Version must be a positive integer.", nameof(version));

        var rate = mode == GoalInterestPolicyMode.None ? 0m : annualRate;
        if (rate < 0)
            throw new ArgumentException("AnnualRate cannot be negative.", nameof(annualRate));

        return new SavingsInterestPolicy
        {
            Id = Guid.NewGuid(),
            PlanType = planType,
            Mode = mode,
            AnnualRate = rate,
            Version = version,
            EffectiveFromUtc = effectiveFromUtc,
            IsActive = true
        };
    }

    /// <summary>
    /// Deactivates this policy version when superseded by a new version.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        DeactivatedAtUtc = DateTime.UtcNow;
    }
}
