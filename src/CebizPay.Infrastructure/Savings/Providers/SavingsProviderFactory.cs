#pragma warning disable CA1848, CS1591
using CebizPay.Application.Common.Interfaces.Savings;
using CebizPay.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Savings.Providers;

/// <summary>
/// Factory implementation for resolving registered ISavingsProvider adapters.
/// </summary>
public sealed class SavingsProviderFactory : ISavingsProviderFactory
{
    private readonly IEnumerable<ISavingsProvider> _providers;
    private readonly SavingsOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="SavingsProviderFactory"/>.
    /// </summary>
    public SavingsProviderFactory(
        IEnumerable<ISavingsProvider> providers,
        IOptions<SavingsOptions> options)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public ISavingsProvider GetProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name is required.", nameof(providerName));

        var provider = _providers.FirstOrDefault(p =>
            string.Equals(p.ProviderName, providerName.Trim(), StringComparison.OrdinalIgnoreCase));

        return provider ?? throw new InvalidOperationException(
            $"Savings provider '{providerName}' is not registered. Available providers: {string.Join(", ", _providers.Select(p => p.ProviderName))}.");
    }

    /// <inheritdoc/>
    public ISavingsProvider GetActiveProvider()
    {
        var targetName = string.IsNullOrWhiteSpace(_options.ActiveProvider) ? "Mock" : _options.ActiveProvider;
        return GetProvider(targetName);
    }
}
