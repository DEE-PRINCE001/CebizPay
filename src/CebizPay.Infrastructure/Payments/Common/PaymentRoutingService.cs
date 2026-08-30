using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Payments.Flutterwave;
using CebizPay.Infrastructure.Payments.Monnify;
using CebizPay.Infrastructure.Payments.Paystack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Payments.Common;

/// <summary>
/// Infrastructure implementation of <see cref="IPaymentRoutingService"/> providing capability-aware,
/// prioritized provider selection and sequential fallback resolution based on runtime configuration.
/// </summary>
public sealed class PaymentRoutingService : IPaymentRoutingService
{
    private readonly IOptions<FlutterwaveOptions> _flutterwaveOptions;
    private readonly IOptions<PaystackOptions> _paystackOptions;
    private readonly IOptions<MonnifyOptions> _monnifyOptions;
    private readonly ILogger<PaymentRoutingService> _logger;

    private static readonly Dictionary<PaymentCapability, IReadOnlyList<PaymentProvider>> DefaultCapabilityPriorities =
        new()
        {
            [PaymentCapability.VirtualAccount] = new[]
            {
                PaymentProvider.Monnify
            },
            [PaymentCapability.CardFunding] = new[]
            {
                PaymentProvider.Flutterwave,
                PaymentProvider.Paystack
            },
            [PaymentCapability.BankTransfer] = new[]
            {
                PaymentProvider.Monnify,
                PaymentProvider.Flutterwave,
                PaymentProvider.Paystack
            },
            [PaymentCapability.BankAccountResolution] = new[]
            {
                PaymentProvider.Monnify,
                PaymentProvider.Flutterwave,
                PaymentProvider.Paystack
            }
        };

    /// <summary>
    /// Initializes a new instance of <see cref="PaymentRoutingService"/> with default enabled options for testing.
    /// </summary>
    public PaymentRoutingService()
        : this(
            Microsoft.Extensions.Options.Options.Create(new FlutterwaveOptions { Enabled = true }),
            Microsoft.Extensions.Options.Options.Create(new PaystackOptions { Enabled = true }),
            Microsoft.Extensions.Options.Options.Create(new MonnifyOptions { Enabled = true }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentRoutingService>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentRoutingService"/> class.
    /// </summary>
    public PaymentRoutingService(
        IOptions<FlutterwaveOptions> flutterwaveOptions,
        IOptions<PaystackOptions> paystackOptions,
        IOptions<MonnifyOptions> monnifyOptions,
        ILogger<PaymentRoutingService> logger)
    {
        _flutterwaveOptions = flutterwaveOptions ?? throw new ArgumentNullException(nameof(flutterwaveOptions));
        _paystackOptions = paystackOptions ?? throw new ArgumentNullException(nameof(paystackOptions));
        _monnifyOptions = monnifyOptions ?? throw new ArgumentNullException(nameof(monnifyOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public bool IsProviderEnabled(PaymentProvider provider) => provider switch
    {
        PaymentProvider.Flutterwave => _flutterwaveOptions.Value?.Enabled ?? false,
        PaymentProvider.Paystack => _paystackOptions.Value?.Enabled ?? false,
        PaymentProvider.Monnify => _monnifyOptions.Value?.Enabled ?? false,
        _ => false
    };

    /// <inheritdoc/>
    public IReadOnlyList<PaymentProvider> GetRoute(PaymentCapability capability)
    {
        if (!DefaultCapabilityPriorities.TryGetValue(capability, out var candidatePriorities))
        {
            return Array.Empty<PaymentProvider>();
        }

        return candidatePriorities
            .Where(IsProviderEnabled)
            .ToList();
    }

    /// <inheritdoc/>
    public PaymentProvider ResolvePrimaryProvider(PaymentCapability capability)
    {
        var route = GetRoute(capability);

        if (route.Count == 0)
        {
            throw new InvalidOperationException(
                $"No enabled payment provider is currently available for capability '{capability}'. " +
                $"Check configuration options for registered providers.");
        }

        return route[0];
    }

    /// <inheritdoc/>
    public PaymentProvider? GetNextFallbackProvider(PaymentCapability capability, PaymentProvider currentProvider)
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

        // If current provider is found and there is a subsequent enabled provider in the route
        if (currentIndex >= 0 && currentIndex + 1 < route.Count)
        {
            return route[currentIndex + 1];
        }

        return null;
    }
}
