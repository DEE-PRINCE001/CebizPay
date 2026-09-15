#pragma warning disable CA1848
using CebizPay.Application.Common.Interfaces.Caching;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Infrastructure.Payments.Paystack;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Payments.Common;

/// <summary>
/// Infrastructure service that resolves and caches the directory of Nigerian banks
/// using Paystack with Redis/distributed caching and a fallback static directory.
/// </summary>
public sealed class BankDirectoryService : IBankDirectoryService
{
    private const string CacheKey = "directory:banks:ng";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private readonly PaystackClient _paystackClient;
    private readonly ICacheService _cacheService;
    private readonly ILogger<BankDirectoryService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="BankDirectoryService"/>.
    /// </summary>
    public BankDirectoryService(
        PaystackClient paystackClient,
        ICacheService cacheService,
        ILogger<BankDirectoryService> logger)
    {
        _paystackClient = paystackClient ?? throw new ArgumentNullException(nameof(paystackClient));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BankDto>> GetBanksAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cached = await _cacheService.GetAsync<List<BankDto>>(CacheKey, cancellationToken).ConfigureAwait(false);
            if (cached != null && cached.Count > 0)
            {
                return cached;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read banks from cache. Proceeding to provider lookup.");
        }

        try
        {
            var paystackBanks = await _paystackClient.GetBanksAsync(cancellationToken).ConfigureAwait(false);
            if (paystackBanks != null && paystackBanks.Count > 0)
            {
                var banks = paystackBanks
                    .Where(b => b.Active != false)
                    .Select(b => new BankDto(
                        Name: b.Name.Trim(),
                        Code: b.Code.Trim(),
                        Slug: b.Slug?.Trim(),
                        LongCode: b.LongCode?.Trim(),
                        Gateway: b.Gateway?.Trim(),
                        Active: b.Active ?? true))
                    .OrderBy(b => b.Name)
                    .ToList();

                try
                {
                    await _cacheService.SetAsync(CacheKey, banks, CacheTtl, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache bank directory list.");
                }

                return banks;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query bank directory from Paystack. Serving fallback list.");
        }

        return GetFallbackBanks();
    }

    private static IReadOnlyList<BankDto> GetFallbackBanks()
    {
        return
        [
            new BankDto("Access Bank", "044", "access-bank", "044150149", null, true),
            new BankDto("Citibank Nigeria", "023", "citibank-nigeria", "023150005", null, true),
            new BankDto("Ecobank Nigeria", "050", "ecobank-nigeria", "050150010", null, true),
            new BankDto("Fidelity Bank", "070", "fidelity-bank", "070150003", null, true),
            new BankDto("First Bank of Nigeria", "011", "first-bank-of-nigeria", "011151003", null, true),
            new BankDto("First City Monument Bank", "214", "first-city-monument-bank", "214150018", null, true),
            new BankDto("Guaranty Trust Bank", "058", "guaranty-trust-bank", "058152036", null, true),
            new BankDto("Heritage Bank", "030", "heritage-bank", "030159992", null, true),
            new BankDto("Keystone Bank", "082", "keystone-bank", "082150017", null, true),
            new BankDto("Kuda Bank", "50211", "kuda-bank", null, null, true),
            new BankDto("Moniepoint MFB", "50515", "moniepoint-mfb-ng", null, null, true),
            new BankDto("OPay Digital Services", "999992", "opay", null, null, true),
            new BankDto("PalmPay", "999991", "palmpay", null, null, true),
            new BankDto("Polaris Bank", "076", "polaris-bank", "076151006", null, true),
            new BankDto("Providus Bank", "101", "providus-bank", "101150001", null, true),
            new BankDto("Stanbic IBTC Bank", "221", "stanbic-ibtc-bank", "221159522", null, true),
            new BankDto("Standard Chartered Bank", "068", "standard-chartered-bank", "068150015", null, true),
            new BankDto("Sterling Bank", "232", "sterling-bank", "232150016", null, true),
            new BankDto("Union Bank of Nigeria", "032", "union-bank-of-nigeria", "032150010", null, true),
            new BankDto("United Bank For Africa", "033", "united-bank-for-africa", "033153513", null, true),
            new BankDto("Unity Bank", "215", "unity-bank", "215154097", null, true),
            new BankDto("Wema Bank", "035", "wema-bank", "035150103", null, true),
            new BankDto("Zenith Bank", "057", "zenith-bank", "057150013", null, true)
        ];
    }
}
