using CebizPay.Domain.Loans.Enums;

namespace CebizPay.Domain.Loans.Entities;

/// <summary>
/// Domain policy aggregate representing default commercial parameters for Standard Individual Loans.
/// Used during offboarding conversion when staff members depart and payroll loans transition to direct individual obligations.
/// </summary>
public class StandardIndividualLoanPolicy
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Policy name / code.</summary>
    public string PolicyName { get; private set; } = "Default Standard Individual Loan Policy";

    /// <summary>Annual flat interest rate applicable to individual loans (e.g. 0.15m for 15% p.a.).</summary>
    public decimal AnnualInterestRate { get; private set; } = 0.15m;

    /// <summary>Default repayment frequency (Monthly).</summary>
    public RepaymentFrequency RepaymentFrequency { get; private set; } = RepaymentFrequency.Monthly;

    /// <summary>Maximum allowed duration in months.</summary>
    public int MaximumDurationMonths { get; private set; } = 24;

    /// <summary>Indicates if policy is currently active.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last update timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    private StandardIndividualLoanPolicy() { } // EF Core

    /// <summary>
    /// Creates a new standard individual loan policy.
    /// </summary>
    public static StandardIndividualLoanPolicy Create(
        string policyName,
        decimal annualInterestRate,
        int maximumDurationMonths,
        RepaymentFrequency repaymentFrequency = RepaymentFrequency.Monthly)
    {
        if (string.IsNullOrWhiteSpace(policyName))
            throw new ArgumentException("PolicyName is required.", nameof(policyName));
        if (annualInterestRate < 0)
            throw new ArgumentException("AnnualInterestRate cannot be negative.", nameof(annualInterestRate));
        if (maximumDurationMonths <= 0)
            throw new ArgumentException("MaximumDurationMonths must be positive.", nameof(maximumDurationMonths));

        return new StandardIndividualLoanPolicy
        {
            Id = Guid.NewGuid(),
            PolicyName = policyName.Trim(),
            AnnualInterestRate = annualInterestRate,
            MaximumDurationMonths = maximumDurationMonths,
            RepaymentFrequency = repaymentFrequency,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
