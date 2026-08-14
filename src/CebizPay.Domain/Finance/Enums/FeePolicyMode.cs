namespace CebizPay.Domain.Finance.Enums;

/// <summary>
/// Peer transfer fee policy mode.
/// </summary>
public enum FeePolicyMode
{
    /// <summary>No fee is charged on peer transfers.</summary>
    Free = 1,
    /// <summary>Fee is calculated as a percentage of the transfer amount, subject to minimum and maximum caps.</summary>
    Percentage = 2
}
