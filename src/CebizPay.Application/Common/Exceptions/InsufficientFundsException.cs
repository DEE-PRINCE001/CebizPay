namespace CebizPay.Application.Common.Exceptions;

/// <summary>
/// Thrown when a transfer is attempted but the sender's available balance is insufficient.
/// Maps to HTTP 422 Unprocessable Entity with code WALLET_INSUFFICIENT_FUNDS.
/// </summary>
public sealed class InsufficientFundsException : Exception
{
    /// <summary>Stable machine-readable error code.</summary>
    public string Code { get; } = "WALLET_INSUFFICIENT_FUNDS";

    /// <summary>
    /// Initializes a new instance of <see cref="InsufficientFundsException"/>.
    /// </summary>
    public InsufficientFundsException(decimal available, decimal required)
        : base($"Insufficient wallet balance. Available: {available:G}, Required: {required:G}.")
    {
    }
}
