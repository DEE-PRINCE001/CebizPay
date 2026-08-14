namespace CebizPay.Application.Common.Exceptions;

/// <summary>
/// Thrown when a sender attempts to transfer funds to their own wallet.
/// Maps to HTTP 422 Unprocessable Entity with code TRANSFER_SELF_NOT_ALLOWED.
/// </summary>
public sealed class SelfTransferException : Exception
{
    /// <summary>Stable machine-readable error code.</summary>
    public string Code { get; } = "TRANSFER_SELF_NOT_ALLOWED";

    /// <summary>
    /// Initializes a new instance of <see cref="SelfTransferException"/>.
    /// </summary>
    public SelfTransferException()
        : base("Self-transfer is not permitted. The source and recipient wallets must be different.")
    {
    }
}
