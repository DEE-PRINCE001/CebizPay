namespace CebizPay.Domain.Finance.Enums;

/// <summary>
/// Specifies the party responsible for platform fees and the settlement flow.
/// </summary>
public enum FeeBearer
{
    /// <summary>
    /// Customer pays requested amount plus platform fee.
    /// Beneficiary receives requested amount.
    /// </summary>
    CustomerPays = 1,

    /// <summary>
    /// Fee is deducted directly from gross incoming funds.
    /// Beneficiary receives net amount (gross - fee).
    /// </summary>
    DeductFromFunds = 2,

    /// <summary>
    /// Platform absorbs the entire fee/cost.
    /// Customer funds requested amount; platform bears operational cost.
    /// </summary>
    PlatformAbsorbs = 3
}
