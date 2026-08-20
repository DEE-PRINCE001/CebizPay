using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Result of a provider failover evaluation and execution attempt.
/// </summary>
public sealed record PaymentFailoverResult(
    bool Succeeded,
    Guid? FallbackAttemptId,
    PaymentProvider? FallbackProvider,
    string? ErrorMessage,
    PaymentProviderResultStatus? ResultStatus = null)
{
    /// <summary>Creates a successful failover result.</summary>
    public static PaymentFailoverResult Success(Guid fallbackAttemptId, PaymentProvider fallbackProvider, PaymentProviderResultStatus status) =>
        new(true, fallbackAttemptId, fallbackProvider, null, status);

    /// <summary>Creates a failed failover evaluation or execution result.</summary>
    public static PaymentFailoverResult Failure(string errorMessage) =>
        new(false, null, null, errorMessage);
}
