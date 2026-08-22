namespace CebizPay.Domain.Payments.Enums;

/// <summary>
/// Channel used for wallet funding.
/// </summary>
public enum FundingChannel
{
    /// <summary>Inbound bank transfer to a dedicated/dynamic virtual account.</summary>
    VirtualAccount = 1,
    /// <summary>Direct card payment / checkout charge.</summary>
    Card = 2
}
