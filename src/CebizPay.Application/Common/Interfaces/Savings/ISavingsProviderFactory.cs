namespace CebizPay.Application.Common.Interfaces.Savings;

/// <summary>
/// Factory contract for resolving the appropriate <see cref="ISavingsProvider"/> instance.
/// </summary>
public interface ISavingsProviderFactory
{
    /// <summary>
    /// Resolves a provider by its unique name (e.g. "Cowrywise", "Anchor", "Mock"), case-insensitively.
    /// </summary>
    ISavingsProvider GetProvider(string providerName);

    /// <summary>
    /// Resolves the currently configured active / default savings provider.
    /// </summary>
    ISavingsProvider GetActiveProvider();
}
