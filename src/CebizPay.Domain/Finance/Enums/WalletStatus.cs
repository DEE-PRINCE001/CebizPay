namespace CebizPay.Domain.Finance.Enums;

/// <summary>
/// Status of a financial wallet.
/// </summary>
public enum WalletStatus
{
    /// <summary>Active wallet capable of transactions.</summary>
    Active = 1,
    /// <summary>Frozen wallet with blocked transactions.</summary>
    Frozen = 2,
    /// <summary>Closed wallet.</summary>
    Closed = 3
}
