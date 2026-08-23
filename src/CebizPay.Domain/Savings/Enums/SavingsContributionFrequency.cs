namespace CebizPay.Domain.Savings.Enums;

/// <summary>
/// Contribution frequency for goal-based or recurring savings plans.
/// </summary>
public enum SavingsContributionFrequency
{
    /// <summary>Daily recurring contribution.</summary>
    Daily = 1,

    /// <summary>Weekly recurring contribution.</summary>
    Weekly = 2,

    /// <summary>Monthly recurring contribution.</summary>
    Monthly = 3
}
