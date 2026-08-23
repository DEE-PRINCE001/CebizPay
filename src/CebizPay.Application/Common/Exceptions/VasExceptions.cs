namespace CebizPay.Application.Common.Exceptions;

/// <summary>
/// Thrown when a duplicate purchase is attempted for the same recipient and product within the 120-second duplicate window.
/// </summary>
public sealed class VasDuplicatePurchaseException : Exception
{
    /// <summary>Canonical machine error code.</summary>
    public const string ErrorCode = "VAS_DUPLICATE_PURCHASE_WINDOW";

    /// <summary>Machine-readable error code.</summary>
    public string Code { get; } = ErrorCode;

    /// <summary>
    /// Initializes a new instance of <see cref="VasDuplicatePurchaseException"/>.
    /// </summary>
    public VasDuplicatePurchaseException(string message = "A purchase of the same amount for this phone number was initiated within the last 120 seconds. Please wait before retrying.")
        : base(message)
    {
    }
}

/// <summary>
/// Thrown when a VAS purchase amount violates product limits (e.g. Airtime minimum ₦50, maximum ₦50,000).
/// </summary>
public sealed class VasLimitExceededException : Exception
{
    /// <summary>Machine-readable error code.</summary>
    public string Code { get; } = "VAS_LIMIT_EXCEEDED";

    /// <summary>
    /// Initializes a new instance of <see cref="VasLimitExceededException"/>.
    /// </summary>
    public VasLimitExceededException(string message) : base(message)
    {
    }
}

/// <summary>
/// Thrown when a specified VAS bundle or product code is invalid or unavailable.
/// </summary>
public sealed class VasInvalidProductException : Exception
{
    /// <summary>Machine-readable error code.</summary>
    public string Code { get; } = "VAS_INVALID_PRODUCT";

    /// <summary>
    /// Initializes a new instance of <see cref="VasInvalidProductException"/>.
    /// </summary>
    public VasInvalidProductException(string message) : base(message)
    {
    }
}
