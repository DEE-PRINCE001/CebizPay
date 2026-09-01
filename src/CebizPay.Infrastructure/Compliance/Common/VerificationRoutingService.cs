#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Infrastructure.Compliance.Dojah;
using CebizPay.Infrastructure.Compliance.Ninja;
using CebizPay.Infrastructure.Compliance.SmileId;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Compliance.Common;

/// <summary>
/// Centralized capability routing service determining primary and fallback compliance verification providers.
/// </summary>
public sealed class VerificationRoutingService : IVerificationRoutingService
{
    private readonly DojahOptions _dojahOptions;
    private readonly SmileIdOptions _smileIdOptions;
    private readonly NinjaOptions _ninjaOptions;

    private static readonly Dictionary<VerificationCapability, List<VerificationProvider>> DefaultRoutes = new()
    {
        [VerificationCapability.Identity] = new() { VerificationProvider.Dojah, VerificationProvider.SmileId, VerificationProvider.Ninja },
        [VerificationCapability.Biometrics] = new() { VerificationProvider.SmileId, VerificationProvider.Dojah },
        [VerificationCapability.Document] = new() { VerificationProvider.SmileId, VerificationProvider.Dojah },
        [VerificationCapability.AmlScreening] = new() { VerificationProvider.Dojah, VerificationProvider.SmileId, VerificationProvider.Ninja },
        [VerificationCapability.Business] = new() { VerificationProvider.Dojah, VerificationProvider.Ninja, VerificationProvider.SmileId },
        [VerificationCapability.BeneficialOwnership] = new() { VerificationProvider.Dojah, VerificationProvider.SmileId, VerificationProvider.Ninja }
    };

    public VerificationRoutingService(
        IOptions<DojahOptions> dojahOptions,
        IOptions<SmileIdOptions> smileIdOptions,
        IOptions<NinjaOptions> ninjaOptions)
    {
        _dojahOptions = dojahOptions?.Value ?? new DojahOptions();
        _smileIdOptions = smileIdOptions?.Value ?? new SmileIdOptions();
        _ninjaOptions = ninjaOptions?.Value ?? new NinjaOptions();
    }

    /// <summary>
    /// Default constructor for testing.
    /// </summary>
    public VerificationRoutingService()
    {
        _dojahOptions = new DojahOptions { Enabled = true };
        _smileIdOptions = new SmileIdOptions { Enabled = true };
        _ninjaOptions = new NinjaOptions { Enabled = true };
    }

    public bool IsProviderEnabled(VerificationProvider provider) =>
        provider switch
        {
            VerificationProvider.Dojah => _dojahOptions.Enabled,
            VerificationProvider.SmileId => _smileIdOptions.Enabled,
            VerificationProvider.Ninja => _ninjaOptions.Enabled,
            VerificationProvider.Internal => true,
            _ => false
        };

    public IReadOnlyList<VerificationProvider> GetRoute(VerificationCapability capability)
    {
        if (!DefaultRoutes.TryGetValue(capability, out var baseRoute))
        {
            return Array.Empty<VerificationProvider>();
        }

        // Return only enabled providers in prioritized order
        return baseRoute.Where(IsProviderEnabled).ToList();
    }

    public VerificationProvider ResolvePrimaryProvider(VerificationCapability capability)
    {
        var route = GetRoute(capability);
        if (route.Count == 0)
        {
            // If all configured providers are disabled in test or config, default to primary in route
            if (DefaultRoutes.TryGetValue(capability, out var defaultRoute) && defaultRoute.Count > 0)
            {
                return defaultRoute[0];
            }

            throw new InvalidOperationException($"No enabled verification provider available for capability '{capability}'.");
        }

        return route[0];
    }

    public VerificationProvider? GetNextFallbackProvider(VerificationCapability capability, VerificationProvider currentProvider)
    {
        var route = GetRoute(capability);
        var currentIndex = -1;
        for (var i = 0; i < route.Count; i++)
        {
            if (route[i] == currentProvider)
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex >= 0 && currentIndex + 1 < route.Count)
        {
            return route[currentIndex + 1];
        }

        return null;
    }
}
