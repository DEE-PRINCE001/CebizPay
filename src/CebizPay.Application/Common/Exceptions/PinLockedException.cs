namespace CebizPay.Application.Common.Exceptions;

/// <summary>
/// Thrown when the transaction PIN is locked due to too many failed verification attempts.
/// The debit lock lasts 15 minutes after 3 consecutive failed PIN attempts.
/// Maps to HTTP 423 Locked with code TRANSFER_PIN_LOCKED.
/// </summary>
public sealed class PinLockedException : Exception
{
    /// <summary>Stable machine-readable error code.</summary>
    public string Code { get; } = "TRANSFER_PIN_LOCKED";

    /// <summary>
    /// Initializes a new instance of <see cref="PinLockedException"/>.
    /// </summary>
    public PinLockedException(string message)
        : base(message)
    {
    }
}
