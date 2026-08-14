namespace CebizPay.Application.Common.Exceptions;

/// <summary>
/// Thrown when a transfer involves a wallet that is not in Active status.
/// Maps to HTTP 422 Unprocessable Entity with code WALLET_NOT_ACTIVE.
/// </summary>
public sealed class WalletNotActiveException : Exception
{
    /// <summary>Stable machine-readable error code.</summary>
    public string Code { get; } = "WALLET_NOT_ACTIVE";

    /// <summary>
    /// Initializes a new instance of <see cref="WalletNotActiveException"/>.
    /// </summary>
    public WalletNotActiveException(string walletRole, string status)
        : base($"{walletRole} wallet is not active (current status: {status}).")
    {
    }
}
