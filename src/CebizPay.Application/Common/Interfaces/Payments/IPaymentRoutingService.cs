using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Capability-aware payment provider router and failover resolution service.
/// Centralizes provider priority selection and availability checks without hardcoding provider choices in business logic.
/// </summary>
public interface IPaymentRoutingService
{
    /// <summary>
    /// Resolves the primary active payment provider for the requested capability based on priority and availability.
    /// </summary>
    PaymentProvider ResolvePrimaryProvider(PaymentCapability capability);

    /// <summary>
    /// Gets the ordered list of enabled providers configured for the requested capability.
    /// </summary>
    IReadOnlyList<PaymentProvider> GetRoute(PaymentCapability capability);

    /// <summary>
    /// Checks whether a specific payment provider is enabled in configuration.
    /// </summary>
    bool IsProviderEnabled(PaymentProvider provider);

    /// <summary>
    /// Resolves the next enabled fallback provider in the capability's routing chain after the specified provider.
    /// Returns null if no further fallback providers are configured or enabled.
    /// </summary>
    PaymentProvider? GetNextFallbackProvider(PaymentCapability capability, PaymentProvider currentProvider);
}
