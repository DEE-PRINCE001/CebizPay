namespace CebizPay.Infrastructure.Savings.Providers.Cowrywise;

/// <summary>
/// HTTP client contract for interacting with the Cowrywise Embed API.
/// </summary>
public interface ICowrywiseClient
{
    /// <summary>Retrieves or refreshes an OAuth2 access token.</summary>
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates or fetches an account for a user.</summary>
    Task<CowrywiseAccountData?> CreateAccountAsync(CowrywiseCreateAccountRequest request, CancellationToken cancellationToken = default);

    /// <summary>Retrieves available rates and tenures for savings products.</summary>
    Task<IReadOnlyList<CowrywiseRateData>> GetRatesAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a new savings contract.</summary>
    Task<CowrywiseSavingsData?> CreateSavingsAsync(CowrywiseCreateSavingsRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deposits funds into a savings contract.</summary>
    Task<CowrywiseFundingData?> FundSavingsAsync(string savingsId, CowrywiseFundSavingsRequest request, CancellationToken cancellationToken = default);

    /// <summary>Retrieves live valuation and status of a savings contract.</summary>
    Task<CowrywisePositionData?> GetPositionAsync(string savingsId, CancellationToken cancellationToken = default);

    /// <summary>Liquidates / redeems a savings contract.</summary>
    Task<CowrywiseLiquidationData?> LiquidateSavingsAsync(string savingsId, CowrywiseLiquidationRequest request, CancellationToken cancellationToken = default);
}
