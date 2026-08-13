namespace CebizPay.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when an idempotency key conflict occurs (same scoped key used with a different request payload).
/// Intercepted by GlobalExceptionHandler to return an RFC 7807 HTTP 409 Conflict ProblemDetails response.
/// </summary>
public sealed class IdempotencyConflictException : Exception
{
    /// <summary>Stable machine-readable error code.</summary>
    public string Code { get; } = "IDEMPOTENCY_KEY_CONFLICT";

    /// <summary>The conflicting idempotency key.</summary>
    public string IdempotencyKey { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="IdempotencyConflictException"/>.
    /// </summary>
    public IdempotencyConflictException(string idempotencyKey, string message)
        : base(message)
    {
        IdempotencyKey = idempotencyKey;
    }
}
