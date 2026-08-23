using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Savings.Enums;

namespace CebizPay.Domain.Savings.Entities;

/// <summary>
/// Domain aggregate root representing a configured Savings Plan.
/// Supports both Individual user-initiated plans and Organization-sponsored corporate savings schemes.
/// </summary>
public class SavingsPlan
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Owning organization ID for corporate plans, or null for individual plans.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Identity user ID of the creator / administrator.</summary>
    public string CreatedByUserId { get; private set; } = string.Empty;

    /// <summary>Ownership type (Individual or Organization).</summary>
    public SavingsOwnerType OwnerType { get; private set; }

    /// <summary>Savings product type (FixedLock or GoalBased).</summary>
    public SavingsPlanType PlanType { get; private set; }

    /// <summary>Human-facing plan title (e.g. 1-Year High Yield Fixed Lock).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Plan description and commercial terms summary.</summary>
    public string? Description { get; private set; }

    /// <summary>Transactional currency.</summary>
    public Currency Currency { get; private set; } = Currency.NGN;

    /// <summary>Annual simple interest rate expressed as a decimal (e.g. 0.10m for 10% per annum).</summary>
    public decimal InterestRate { get; private set; }

    /// <summary>Minimum principal deposit amount.</summary>
    public decimal MinimumAmount { get; private set; }

    /// <summary>Maximum principal deposit amount.</summary>
    public decimal MaximumAmount { get; private set; }

    /// <summary>Minimum lock duration in days (minimum 30 days for FixedLock).</summary>
    public int MinimumDurationDays { get; private set; }

    /// <summary>Maximum lock duration in days (maximum 730 days / 2 years for FixedLock).</summary>
    public int MaximumDurationDays { get; private set; }

    /// <summary>Target savings amount for goal-based plans.</summary>
    public decimal? TargetAmount { get; private set; }

    /// <summary>Scheduled recurring contribution amount for goal-based plans.</summary>
    public decimal? ContributionAmount { get; private set; }

    /// <summary>Scheduled contribution frequency for goal-based plans.</summary>
    public SavingsContributionFrequency? ContributionFrequency { get; private set; }

    /// <summary>Governing interest policy version number.</summary>
    public int InterestPolicyVersion { get; private set; }

    /// <summary>Indicates whether the plan is active for subscriptions.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Plan creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last update timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    private SavingsPlan() { } // EF Core

    /// <summary>
    /// Creates a new Fixed-Lock savings plan adhering to PRD locked boundaries (30-day min, 2-year max, 8%-15% rate).
    /// </summary>
    public static SavingsPlan CreateFixedLockPlan(
        Guid? organizationId,
        string createdByUserId,
        SavingsOwnerType ownerType,
        string name,
        string? description,
        Currency currency,
        decimal interestRate,
        decimal minimumAmount,
        decimal maximumAmount,
        int minimumDurationDays,
        int maximumDurationDays,
        int interestPolicyVersion)
    {
        if (string.IsNullOrWhiteSpace(createdByUserId))
            throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Plan Name is required.", nameof(name));
        if (interestRate < 0.08m || interestRate > 0.15m)
            throw new ArgumentException("Fixed-lock annual interest rate must be between 8% (0.08) and 15% (0.15).", nameof(interestRate));
        if (minimumDurationDays < 30)
            throw new ArgumentException("Fixed-lock minimum duration cannot be less than 30 days.", nameof(minimumDurationDays));
        if (maximumDurationDays > 730)
            throw new ArgumentException("Fixed-lock maximum duration cannot exceed 730 days (2 years).", nameof(maximumDurationDays));
        if (maximumDurationDays < minimumDurationDays)
            throw new ArgumentException("MaximumDurationDays cannot be less than MinimumDurationDays.", nameof(maximumDurationDays));
        if (minimumAmount <= 0)
            throw new ArgumentException("MinimumAmount must be positive.", nameof(minimumAmount));
        if (maximumAmount < minimumAmount)
            throw new ArgumentException("MaximumAmount cannot be less than MinimumAmount.", nameof(maximumAmount));

        return new SavingsPlan
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatedByUserId = createdByUserId,
            OwnerType = ownerType,
            PlanType = SavingsPlanType.FixedLock,
            Name = name.Trim(),
            Description = description?.Trim(),
            Currency = currency,
            InterestRate = interestRate,
            MinimumAmount = minimumAmount,
            MaximumAmount = maximumAmount,
            MinimumDurationDays = minimumDurationDays,
            MaximumDurationDays = maximumDurationDays,
            InterestPolicyVersion = interestPolicyVersion,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new Goal-Based recurring savings plan.
    /// </summary>
    public static SavingsPlan CreateGoalBasedPlan(
        Guid? organizationId,
        string createdByUserId,
        SavingsOwnerType ownerType,
        string name,
        string? description,
        Currency currency,
        decimal targetAmount,
        decimal contributionAmount,
        SavingsContributionFrequency contributionFrequency,
        decimal interestRate,
        int interestPolicyVersion)
    {
        if (string.IsNullOrWhiteSpace(createdByUserId))
            throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Plan Name is required.", nameof(name));
        if (targetAmount <= 0)
            throw new ArgumentException("TargetAmount must be positive.", nameof(targetAmount));
        if (contributionAmount <= 0 || contributionAmount > targetAmount)
            throw new ArgumentException("ContributionAmount must be positive and not exceed TargetAmount.", nameof(contributionAmount));
        if (interestRate < 0)
            throw new ArgumentException("InterestRate cannot be negative.", nameof(interestRate));

        return new SavingsPlan
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatedByUserId = createdByUserId,
            OwnerType = ownerType,
            PlanType = SavingsPlanType.GoalBased,
            Name = name.Trim(),
            Description = description?.Trim(),
            Currency = currency,
            InterestRate = interestRate,
            MinimumAmount = contributionAmount,
            MaximumAmount = targetAmount,
            MinimumDurationDays = 1,
            MaximumDurationDays = 3650,
            TargetAmount = targetAmount,
            ContributionAmount = contributionAmount,
            ContributionFrequency = contributionFrequency,
            InterestPolicyVersion = interestPolicyVersion,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Activates the plan.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the plan, preventing new account openings.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
