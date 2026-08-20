using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Factory abstraction for resolving the appropriate <see cref="IPaymentProvider"/> instance by provider identity.
/// </summary>
public interface IPaymentProviderFactory
{
    /// <summary>
    /// Gets the payment provider implementation for the specified provider type.
    /// </summary>
    IPaymentProvider GetProvider(PaymentProvider provider);
}
