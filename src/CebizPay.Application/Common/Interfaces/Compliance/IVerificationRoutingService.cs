using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Centralized routing service for compliance verification capability routes,
/// primary provider resolution, and safe technical failover chains.
/// </summary>
public interface IVerificationRoutingService
{
    /// <summary>
    /// Returns the ordered active provider route for the specified compliance verification capability.
    /// </summary>
    IReadOnlyList<VerificationProvider> GetRoute(VerificationCapability capability);

    /// <summary>
    /// Resolves the primary active provider for a capability. Throws if no enabled provider is available.
    /// </summary>
    VerificationProvider ResolvePrimaryProvider(VerificationCapability capability);

    /// <summary>
    /// Gets the next enabled fallback provider in the capability chain after a technical failure on the current provider.
    /// Returns null if no further fallbacks exist.
    /// </summary>
    VerificationProvider? GetNextFallbackProvider(VerificationCapability capability, VerificationProvider currentProvider);

    /// <summary>
    /// Checks whether a given provider is enabled in configuration.
    /// </summary>
    bool IsProviderEnabled(VerificationProvider provider);
}
