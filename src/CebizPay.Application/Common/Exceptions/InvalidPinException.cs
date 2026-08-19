namespace CebizPay.Application.Common.Exceptions;

/// <summary>
/// Thrown when the provided transaction PIN is incorrect.
/// Maps to HTTP 400 Bad Request with code INVALID_TRANSACTION_PIN.
/// </summary>
public sealed class InvalidPinException : Exception
{
    /// <summary>Stable machine-readable error code.</summary>
    public string Code { get; } = "INVALID_TRANSACTION_PIN";

    /// <summary>
    /// Initializes a new instance of <see cref="InvalidPinException"/>.
    /// </summary>
    public InvalidPinException(string message)
        : base(message)
    {
    }
}
