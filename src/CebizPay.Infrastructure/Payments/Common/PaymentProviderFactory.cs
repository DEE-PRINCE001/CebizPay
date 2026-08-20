using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Infrastructure.Payments.Common;

/// <summary>
/// Infrastructure implementation of <see cref="IPaymentProviderFactory"/> resolving registered provider adapters.
/// </summary>
public sealed class PaymentProviderFactory : IPaymentProviderFactory
{
    private readonly IEnumerable<IPaymentProvider> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentProviderFactory"/> class.
    /// </summary>
    public PaymentProviderFactory(IEnumerable<IPaymentProvider> providers)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
    }

    /// <inheritdoc/>
    public IPaymentProvider GetProvider(PaymentProvider provider)
    {
        var match = _providers.FirstOrDefault(p => p.Provider == provider);
        return match ?? throw new NotSupportedException($"Payment provider '{provider}' is not configured or registered.");
    }
}
