namespace CebizPay.Domain.Finance.Enums;

/// <summary>
/// Mathematical method used to calculate platform fees.
/// </summary>
public enum FeeCalculationMethod
{
    /// <summary>No fee charged (fee = 0).</summary>
    Free = 1,

    /// <summary>Fixed nominal fee charged regardless of transaction amount.</summary>
    Fixed = 2,

    /// <summary>Proportional percentage fee without ceiling or floor.</summary>
    Percentage = 3,

    /// <summary>Proportional percentage fee subject to configured minimum and maximum caps.</summary>
    PercentageWithCap = 4
}
