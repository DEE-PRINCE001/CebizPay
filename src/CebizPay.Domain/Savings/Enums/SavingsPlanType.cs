namespace CebizPay.Domain.Savings.Enums;

/// <summary>
/// Type of savings product.
/// </summary>
public enum SavingsPlanType
{
    /// <summary>
    /// Fixed-lock savings plan with committed principal, daily interest accrual, and early withdrawal penalties.
    /// </summary>
    FixedLock = 1,

    /// <summary>
    /// Goal-based recurring savings plan toward a designated target balance.
    /// </summary>
    GoalBased = 2
}
