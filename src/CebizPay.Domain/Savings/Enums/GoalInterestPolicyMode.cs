namespace CebizPay.Domain.Savings.Enums;

/// <summary>
/// Super Admin policy mode governing interest calculations on goal-based savings.
/// </summary>
public enum GoalInterestPolicyMode
{
    /// <summary>No interest accrual (0% annual interest rate).</summary>
    None = 1,

    /// <summary>Configured annual interest percentage applies.</summary>
    Percentage = 2
}
