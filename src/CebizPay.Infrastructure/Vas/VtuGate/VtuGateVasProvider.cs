using CebizPay.Application.Common.Interfaces.Vas;
using CebizPay.Application.Common.Models.Vas;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Vas.Enums;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Vas.VtuGate;

/// <summary>
/// Infrastructure adapter implementing <see cref="IVasProvider"/> for VTUGATE.
/// Maps domain concepts to VTUGATE wire protocols and classifies results into neutral outcome models.
/// </summary>
public sealed partial class VtuGateVasProvider : IVasProvider
{
    private readonly VtuGateClient _client;
    private readonly ILogger<VtuGateVasProvider> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="VtuGateVasProvider"/>.
    /// </summary>
    public VtuGateVasProvider(VtuGateClient client, ILogger<VtuGateVasProvider> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public VasProvider Provider => VasProvider.VtuGate;

    /// <inheritdoc/>
    public async Task<VasOperatorResolutionResult> ResolveOperatorAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        var response = await _client.ResolveOperatorAsync(phoneNumber, cancellationToken).ConfigureAwait(false);

        if (IsSuccessStatus(response.Status) && !string.IsNullOrWhiteSpace(response.Message))
        {
            var network = ParseNetwork(response.Message);
            if (network.HasValue)
            {
                return VasOperatorResolutionResult.Success(network.Value);
            }
        }

        return VasOperatorResolutionResult.Failure(response.Message ?? "Operator resolution failed.");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DataBundleDto>> GetDataBundlesAsync(VasNetwork? network = null, CancellationToken cancellationToken = default)
    {
        var providerBundles = await _client.GetDataBundlesAsync(cancellationToken).ConfigureAwait(false);

        var list = new List<DataBundleDto>();

        if (providerBundles.Count > 0)
        {
            foreach (var b in providerBundles)
            {
                var net = ParseNetwork(b.Network);
                if (net.HasValue && (!network.HasValue || network.Value == net.Value))
                {
                    list.Add(new DataBundleDto(
                        ProductCode: b.PlanId,
                        Network: net.Value,
                        Name: b.Name,
                        Volume: b.Volume ?? string.Empty,
                        Validity: b.Validity ?? "30 Days",
                        Amount: b.Amount,
                        Currency: Currency.NGN));
                }
            }
        }

        // If provider returned empty catalog (e.g. mock/sandbox/offline), provide standard catalog fallback
        if (list.Count == 0)
        {
            list.AddRange(GetStandardCatalog(network));
        }

        return list;
    }

    /// <inheritdoc/>
    public async Task<VasPurchaseProviderResult> PurchaseAirtimeAsync(
        string reference,
        string phoneNumber,
        VasNetwork network,
        decimal amount,
        Currency currency,
        CancellationToken cancellationToken = default)
    {
        var networkCode = FormatNetworkCode(network);
        var response = await _client.PurchaseAirtimeAsync(reference, phoneNumber, networkCode, amount, cancellationToken).ConfigureAwait(false);

        return ClassifyResponse(response, reference);
    }

    /// <inheritdoc/>
    public async Task<VasPurchaseProviderResult> PurchaseDataAsync(
        string reference,
        string phoneNumber,
        VasNetwork network,
        string productCode,
        decimal amount,
        Currency currency,
        CancellationToken cancellationToken = default)
    {
        var networkCode = FormatNetworkCode(network);
        var response = await _client.PurchaseDataAsync(reference, phoneNumber, networkCode, productCode, amount, cancellationToken).ConfigureAwait(false);

        return ClassifyResponse(response, reference);
    }

    /// <inheritdoc/>
    public async Task<VasPurchaseProviderResult> GetTransactionStatusAsync(
        string reference,
        string? providerReference,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.GetTransactionStatusAsync(reference, providerReference, cancellationToken).ConfigureAwait(false);
        return ClassifyResponse(response, reference);
    }

    private static VasPurchaseProviderResult ClassifyResponse(DTOs.VtuGateResponse response, string fallbackReference)
    {
        var status = response.Status?.ToLowerInvariant() ?? "unknown";
        var providerRef = response.Reference ?? response.TransactionId ?? fallbackReference;

        if (status is "success" or "successful" or "delivered" or "completed")
        {
            return VasPurchaseProviderResult.Success(providerRef, response.Message);
        }

        if (status is "failed" or "rejected" or "invalid_number" or "inactive_recipient")
        {
            return VasPurchaseProviderResult.BusinessFailure(
                response.Code ?? "BUSINESS_FAILURE",
                response.Message ?? "Fulfillment was rejected by telecommunications provider.");
        }

        if (status is "error" or "server_error" or "gateway_timeout")
        {
            return VasPurchaseProviderResult.TechnicalFailure(
                response.Code ?? "TECHNICAL_FAILURE",
                response.Message ?? "Provider technical error occurred.");
        }

        // Unknown / Timeout / In-flight
        return VasPurchaseProviderResult.Unknown(
            response.Message ?? "Fulfillment outcome is currently unknown/in-flight.");
    }

    private static bool IsSuccessStatus(string? status) =>
        status?.Equals("success", StringComparison.OrdinalIgnoreCase) == true ||
        status?.Equals("successful", StringComparison.OrdinalIgnoreCase) == true;

    private static string FormatNetworkCode(VasNetwork network) => network switch
    {
        VasNetwork.Mtn => "MTN",
        VasNetwork.Airtel => "AIRTEL",
        VasNetwork.Glo => "GLO",
        VasNetwork.NineMobile => "9MOBILE",
        _ => network.ToString().ToUpperInvariant()
    };

    private static VasNetwork? ParseNetwork(string? networkStr)
    {
        if (string.IsNullOrWhiteSpace(networkStr))
            return null;

        var upper = networkStr.Trim().ToUpperInvariant();
        if (upper.Contains("MTN", StringComparison.Ordinal)) return VasNetwork.Mtn;
        if (upper.Contains("AIRTEL", StringComparison.Ordinal)) return VasNetwork.Airtel;
        if (upper.Contains("GLO", StringComparison.Ordinal)) return VasNetwork.Glo;
        if (upper.Contains("9MOBILE", StringComparison.Ordinal) || upper.Contains("NINEMOBILE", StringComparison.Ordinal) || upper.Contains("ETISALAT", StringComparison.Ordinal))
            return VasNetwork.NineMobile;

        return null;
    }

    private static List<DataBundleDto> GetStandardCatalog(VasNetwork? network)
    {
        var all = new List<DataBundleDto>
        {
            // MTN
            new("MTN-500MB", VasNetwork.Mtn, "MTN 500MB 30-Day SME", "500MB", "30 Days", 150m, Currency.NGN),
            new("MTN-1GB", VasNetwork.Mtn, "MTN 1GB 30-Day SME", "1GB", "30 Days", 280m, Currency.NGN),
            new("MTN-2GB", VasNetwork.Mtn, "MTN 2GB 30-Day SME", "2GB", "30 Days", 560m, Currency.NGN),
            new("MTN-5GB", VasNetwork.Mtn, "MTN 5GB 30-Day SME", "5GB", "30 Days", 1400m, Currency.NGN),
            new("MTN-10GB", VasNetwork.Mtn, "MTN 10GB 30-Day SME", "10GB", "30 Days", 2800m, Currency.NGN),

            // Airtel
            new("AIRTEL-500MB", VasNetwork.Airtel, "Airtel 500MB 30-Day", "500MB", "30 Days", 150m, Currency.NGN),
            new("AIRTEL-1GB", VasNetwork.Airtel, "Airtel 1GB 30-Day", "1GB", "30 Days", 280m, Currency.NGN),
            new("AIRTEL-2GB", VasNetwork.Airtel, "Airtel 2GB 30-Day", "2GB", "30 Days", 560m, Currency.NGN),
            new("AIRTEL-5GB", VasNetwork.Airtel, "Airtel 5GB 30-Day", "5GB", "30 Days", 1400m, Currency.NGN),

            // Glo
            new("GLO-1GB", VasNetwork.Glo, "Glo 1GB 30-Day", "1GB", "30 Days", 250m, Currency.NGN),
            new("GLO-2GB", VasNetwork.Glo, "Glo 2GB 30-Day", "2GB", "30 Days", 500m, Currency.NGN),
            new("GLO-5GB", VasNetwork.Glo, "Glo 5GB 30-Day", "5GB", "30 Days", 1250m, Currency.NGN),

            // 9mobile
            new("9MOBILE-1GB", VasNetwork.NineMobile, "9mobile 1GB 30-Day", "1GB", "30 Days", 280m, Currency.NGN),
            new("9MOBILE-2GB", VasNetwork.NineMobile, "9mobile 2GB 30-Day", "2GB", "30 Days", 560m, Currency.NGN),
            new("9MOBILE-5GB", VasNetwork.NineMobile, "9mobile 5GB 30-Day", "5GB", "30 Days", 1400m, Currency.NGN)
        };

        if (network.HasValue)
        {
            return all.FindAll(x => x.Network == network.Value);
        }

        return all;
    }
}
