namespace CebizPay.Application.Common.Exceptions;

/// <summary>
/// Thrown when a transaction PIN has not been set but is required for the operation.
/// Maps to HTTP 422 Unprocessable Entity with code TRANSFER_PIN_REQUIRED.
/// </summary>
public sealed class PinRequiredException : Exception
{
    /// <summary>Stable machine-readable error code.</summary>
    public string Code { get; } = "TRANSFER_PIN_REQUIRED";

    /// <summary>
    /// Initializes a new instance of <see cref="PinRequiredException"/>.
    /// </summary>
    public PinRequiredException()
        : base("A transaction PIN is required. Please set a 4-digit PIN before initiating transfers.")
    {
    }
}
