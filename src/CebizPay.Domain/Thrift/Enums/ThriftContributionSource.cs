namespace CebizPay.Domain.Thrift.Enums;

/// <summary>
/// Source used for funding a thrift contribution.
/// </summary>
public enum ThriftContributionSource
{
    /// <summary>Automated deduction from member's CebizPay wallet.</summary>
    Wallet = 1,

    /// <summary>Fallback charge against member's tokenized bank card when wallet balance is insufficient.</summary>
    CardFallback = 2
}
