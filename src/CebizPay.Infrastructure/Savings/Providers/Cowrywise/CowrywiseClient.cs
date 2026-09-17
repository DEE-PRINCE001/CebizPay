#pragma warning disable CA1848, CS1591
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CebizPay.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Savings.Providers.Cowrywise;

/// <summary>
/// HTTP client implementation for Cowrywise Embed API.
/// Manages OAuth2 token caching, request serialization, and error handling.
/// </summary>
public sealed class CowrywiseClient : ICowrywiseClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly CowrywiseOptions _options;
    private readonly ILogger<CowrywiseClient> _logger;

    private string? _cachedAccessToken;
    private DateTime _tokenExpiresAtUtc = DateTime.MinValue;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of <see cref="CowrywiseClient"/>.
    /// </summary>
    public CowrywiseClient(
        HttpClient httpClient,
        IOptions<CowrywiseOptions> options,
        ILogger<CowrywiseClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl) && Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            _httpClient.BaseAddress = baseUri;
        }

        if (_options.TimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }
    }

    /// <inheritdoc/>
    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Cowrywise provider is disabled; token request skipped.");
            return null;
        }

        if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && DateTime.UtcNow < _tokenExpiresAtUtc)
        {
            return _cachedAccessToken;
        }

        await _authLock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedAccessToken) && DateTime.UtcNow < _tokenExpiresAtUtc)
            {
                return _cachedAccessToken;
            }

            var requestPayload = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", _options.ClientId },
                { "client_secret", _options.ClientSecret }
            });

            using var response = await _httpClient.PostAsync("auth/o/token/", requestPayload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to obtain Cowrywise token. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorBody);
                return null;
            }

            var tokenResult = await response.Content.ReadFromJsonAsync<CowrywiseAuthTokenResponse>(JsonOptions, cancellationToken);
            if (tokenResult == null || string.IsNullOrWhiteSpace(tokenResult.AccessToken))
            {
                _logger.LogError("Received empty Cowrywise access token.");
                return null;
            }

            _cachedAccessToken = tokenResult.AccessToken;
            var bufferSeconds = Math.Min(60, tokenResult.ExpiresIn / 2);
            _tokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResult.ExpiresIn - bufferSeconds);

            return _cachedAccessToken;
        }
        finally
        {
            _authLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<CowrywiseAccountData?> CreateAccountAsync(CowrywiseCreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            return null;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "accounts/")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Cowrywise account creation failed. Status: {Status}, Error: {Err}", response.StatusCode, err);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<CowrywiseApiResponse<CowrywiseAccountData>>(JsonOptions, cancellationToken);
        return result?.Data;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CowrywiseRateData>> GetRatesAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            return Array.Empty<CowrywiseRateData>();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "savings/rates/");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to fetch Cowrywise savings rates. Status: {Status}", response.StatusCode);
            return Array.Empty<CowrywiseRateData>();
        }

        var result = await response.Content.ReadFromJsonAsync<CowrywiseApiResponse<List<CowrywiseRateData>>>(JsonOptions, cancellationToken);
        return result?.Data ?? (IReadOnlyList<CowrywiseRateData>)Array.Empty<CowrywiseRateData>();
    }

    /// <inheritdoc/>
    public async Task<CowrywiseSavingsData?> CreateSavingsAsync(CowrywiseCreateSavingsRequest request, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            return null;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "savings/")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Cowrywise savings plan creation failed. Status: {Status}, Error: {Err}", response.StatusCode, err);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<CowrywiseApiResponse<CowrywiseSavingsData>>(JsonOptions, cancellationToken);
        return result?.Data;
    }

    /// <inheritdoc/>
    public async Task<CowrywiseFundingData?> FundSavingsAsync(string savingsId, CowrywiseFundSavingsRequest request, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            return null;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"savings/{Uri.EscapeDataString(savingsId)}/deposits/")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Cowrywise savings deposit failed for {SavingsId}. Status: {Status}, Error: {Err}", savingsId, response.StatusCode, err);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<CowrywiseApiResponse<CowrywiseFundingData>>(JsonOptions, cancellationToken);
        return result?.Data;
    }

    /// <inheritdoc/>
    public async Task<CowrywisePositionData?> GetPositionAsync(string savingsId, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            return null;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"savings/{Uri.EscapeDataString(savingsId)}/");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Cowrywise position fetch failed for {SavingsId}. Status: {Status}", savingsId, response.StatusCode);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<CowrywiseApiResponse<CowrywisePositionData>>(JsonOptions, cancellationToken);
        return result?.Data;
    }

    /// <inheritdoc/>
    public async Task<CowrywiseLiquidationData?> LiquidateSavingsAsync(string savingsId, CowrywiseLiquidationRequest request, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            return null;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"investments/{Uri.EscapeDataString(savingsId)}/liquidate/")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Cowrywise liquidation failed for {SavingsId}. Status: {Status}, Error: {Err}", savingsId, response.StatusCode, err);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<CowrywiseApiResponse<CowrywiseLiquidationData>>(JsonOptions, cancellationToken);
        return result?.Data;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _authLock.Dispose();
    }
}
