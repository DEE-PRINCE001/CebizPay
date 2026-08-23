using CebizPay.Application.Common.Models.Vas;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Vas.Enums;

namespace CebizPay.Application.Common.Interfaces.Vas;

/// <summary>
/// Domain/Application interface for Value-Added Services (VAS) gateway adapters (VTUGATE).
/// Keeps Application layer completely decoupled from external provider DTOs and protocols.
/// </summary>
public interface IVasProvider
{
    /// <summary>Identifies the provider gateway.</summary>
    VasProvider Provider { get; }

    /// <summary>
    /// Attempts to automatically detect the mobile telecommunications operator for a recipient phone number.
    /// </summary>
    Task<VasOperatorResolutionResult> ResolveOperatorAsync(string phoneNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current catalog of available mobile data bundles from the provider.
    /// </summary>
    Task<IReadOnlyList<DataBundleDto>> GetDataBundlesAsync(VasNetwork? network = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches an airtime top-up purchase to the provider.
    /// </summary>
    Task<VasPurchaseProviderResult> PurchaseAirtimeAsync(
        string reference,
        string phoneNumber,
        VasNetwork network,
        decimal amount,
        Currency currency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a mobile data bundle purchase to the provider.
    /// </summary>
    Task<VasPurchaseProviderResult> PurchaseDataAsync(
        string reference,
        string phoneNumber,
        VasNetwork network,
        string productCode,
        decimal amount,
        Currency currency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the definitive fulfillment status of a prior transaction from the provider.
    /// </summary>
    Task<VasPurchaseProviderResult> GetTransactionStatusAsync(
        string reference,
        string? providerReference,
        CancellationToken cancellationToken = default);
}
