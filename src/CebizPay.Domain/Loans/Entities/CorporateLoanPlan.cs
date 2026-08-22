using CebizPay.Domain.Loans.Enums;

namespace CebizPay.Domain.Loans.Entities;

/// <summary>
/// Domain aggregate root representing an organization-configured corporate staff loan plan.
/// Defines principal limits, flat interest rates, allowed repayment durations, and minimum eligibility criteria.
/// </summary>
public class CorporateLoanPlan
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Owning organization tenant ID.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Human-facing plan title (e.g. Standard Staff Salary Advance).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Plan description and commercial terms summary.</summary>
    public string? Description { get; private set; }

    /// <summary>Minimum borrowable loan principal amount.</summary>
    public decimal MinimumAmount { get; private set; }

    /// <summary>Maximum borrowable loan principal amount.</summary>
    public decimal MaximumAmount { get; private set; }

    /// <summary>Annual flat/simple interest rate expressed as a decimal (e.g. 0.10m for 10% per annum).</summary>
    public decimal InterestRate { get; private set; }

    /// <summary>Minimum repayment duration in months.</summary>
    public int MinimumDurationMonths { get; private set; }

    /// <summary>Maximum repayment duration in months.</summary>
    public int MaximumDurationMonths { get; private set; }

    /// <summary>Repayment installment frequency (default Monthly).</summary>
    public RepaymentFrequency RepaymentFrequency { get; private set; } = RepaymentFrequency.Monthly;

    /// <summary>Minimum verified monthly salary required for staff eligibility.</summary>
    public decimal MinimumMonthlySalary { get; private set; }

    /// <summary>Indicates whether new applications can be created against this plan.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Plan creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last plan update timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    private CorporateLoanPlan() { } // EF Core

    /// <summary>
    /// Creates a new corporate loan plan.
    /// </summary>
    public static CorporateLoanPlan Create(
        Guid organizationId,
        string name,
        string? description,
        decimal minimumAmount,
        decimal maximumAmount,
        decimal interestRate,
        int minimumDurationMonths,
        int maximumDurationMonths,
        decimal minimumMonthlySalary,
        RepaymentFrequency repaymentFrequency = RepaymentFrequency.Monthly)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Plan Name is required.", nameof(name));
        if (minimumAmount <= 0)
            throw new ArgumentException("MinimumAmount must be positive.", nameof(minimumAmount));
        if (maximumAmount < minimumAmount)
            throw new ArgumentException("MaximumAmount cannot be less than MinimumAmount.", nameof(maximumAmount));
        if (interestRate < 0)
            throw new ArgumentException("InterestRate cannot be negative.", nameof(interestRate));
        if (minimumDurationMonths <= 0)
            throw new ArgumentException("MinimumDurationMonths must be positive.", nameof(minimumDurationMonths));
        if (maximumDurationMonths < minimumDurationMonths)
            throw new ArgumentException("MaximumDurationMonths cannot be less than MinimumDurationMonths.", nameof(maximumDurationMonths));
        if (minimumMonthlySalary < 0)
            throw new ArgumentException("MinimumMonthlySalary cannot be negative.", nameof(minimumMonthlySalary));

        return new CorporateLoanPlan
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name.Trim(),
            Description = description?.Trim(),
            MinimumAmount = minimumAmount,
            MaximumAmount = maximumAmount,
            InterestRate = interestRate,
            MinimumDurationMonths = minimumDurationMonths,
            MaximumDurationMonths = maximumDurationMonths,
            RepaymentFrequency = repaymentFrequency,
            MinimumMonthlySalary = minimumMonthlySalary,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Updates loan plan commercial parameters and metadata.
    /// </summary>
    public void UpdateDetails(
        string name,
        string? description,
        decimal minimumAmount,
        decimal maximumAmount,
        decimal interestRate,
        int minimumDurationMonths,
        int maximumDurationMonths,
        decimal minimumMonthlySalary,
        bool isActive,
        RepaymentFrequency repaymentFrequency = RepaymentFrequency.Monthly)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Plan Name is required.", nameof(name));
        if (minimumAmount <= 0)
            throw new ArgumentException("MinimumAmount must be positive.", nameof(minimumAmount));
        if (maximumAmount < minimumAmount)
            throw new ArgumentException("MaximumAmount cannot be less than MinimumAmount.", nameof(maximumAmount));
        if (interestRate < 0)
            throw new ArgumentException("InterestRate cannot be negative.", nameof(interestRate));
        if (minimumDurationMonths <= 0)
            throw new ArgumentException("MinimumDurationMonths must be positive.", nameof(minimumDurationMonths));
        if (maximumDurationMonths < minimumDurationMonths)
            throw new ArgumentException("MaximumDurationMonths cannot be less than MinimumDurationMonths.", nameof(maximumDurationMonths));
        if (minimumMonthlySalary < 0)
            throw new ArgumentException("MinimumMonthlySalary cannot be negative.", nameof(minimumMonthlySalary));

        Name = name.Trim();
        Description = description?.Trim();
        MinimumAmount = minimumAmount;
        MaximumAmount = maximumAmount;
        InterestRate = interestRate;
        MinimumDurationMonths = minimumDurationMonths;
        MaximumDurationMonths = maximumDurationMonths;
        RepaymentFrequency = repaymentFrequency;
        MinimumMonthlySalary = minimumMonthlySalary;
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Activates the plan for staff applications.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the plan, preventing new loan applications.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Validates proposed loan parameters against plan bounds and salary threshold.
    /// </summary>
    public (bool IsValid, string? ErrorMessage) ValidateEligibility(decimal requestedAmount, int durationMonths, decimal verifiedSalary)
    {
        if (!IsActive)
            return (false, "Corporate loan plan is currently inactive.");
        if (requestedAmount < MinimumAmount || requestedAmount > MaximumAmount)
            return (false, $"Requested amount ({requestedAmount:N2}) must be between {MinimumAmount:N2} and {MaximumAmount:N2}.");
        if (durationMonths < MinimumDurationMonths || durationMonths > MaximumDurationMonths)
            return (false, $"Duration ({durationMonths} months) must be between {MinimumDurationMonths} and {MaximumDurationMonths} months.");
        if (verifiedSalary < MinimumMonthlySalary)
            return (false, $"Verified monthly salary ({verifiedSalary:N2}) is below the required plan threshold ({MinimumMonthlySalary:N2}).");

        return (true, null);
    }
}
