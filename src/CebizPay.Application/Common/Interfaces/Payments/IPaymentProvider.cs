using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Application abstraction boundary for external payment providers (Flutterwave, Paystack).
/// Expresses provider capabilities in a provider-neutral manner without leaking provider SDKs.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>Gets the unique provider identity.</summary>
    PaymentProvider Provider { get; }

    /// <summary>
    /// Initializes or dispatches a payment attempt to the external payment gateway.
    /// </summary>
    Task<PaymentProviderResult> InitializePaymentAsync(PaymentAttempt attempt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the current status of a payment attempt from the external payment gateway using the provider reference.
    /// </summary>
    Task<PaymentProviderResult> GetPaymentStatusAsync(string providerReference, CancellationToken cancellationToken = default);
}
