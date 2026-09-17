#pragma warning disable CA1848, CS1591
using System.Net.Http.Json;
using System.Text.Json;
using CebizPay.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Savings.Providers.Anchor;

/// <summary>
/// HTTP client implementation for Anchor BaaS API.
/// Manages JSON:API serialization, header authentication, and error handling.
/// </summary>
public sealed class AnchorClient : IAnchorClient
{
    private readonly HttpClient _httpClient;
    private readonly AnchorOptions _options;
    private readonly ILogger<AnchorClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of <see cref="AnchorClient"/>.
    /// </summary>
    public AnchorClient(
        HttpClient httpClient,
        IOptions<AnchorOptions> options,
        ILogger<AnchorClient> logger)
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

    private void ApplyHeaders(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Remove("x-anchor-key");
            request.Headers.Add("x-anchor-key", _options.ApiKey);
        }
    }

    /// <inheritdoc/>
    public async Task<AnchorResource<AnchorIndividualCustomerAttributes>?> CreateCustomerAsync(
        AnchorResourceEnvelope<AnchorResource<AnchorIndividualCustomerAttributes>> request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Anchor provider is disabled; customer creation skipped.");
            return null;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "customers/individuals")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        ApplyHeaders(httpRequest);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Anchor customer creation failed. Status: {Status}, Error: {Err}", response.StatusCode, err);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<AnchorResourceEnvelope<AnchorResource<AnchorIndividualCustomerAttributes>>>(JsonOptions, cancellationToken);
        return result?.Data;
    }

    /// <inheritdoc/>
    public async Task<AnchorResource<AnchorSubAccountAttributes>?> CreateSubAccountAsync(
        AnchorResourceEnvelope<AnchorResource<AnchorSubAccountAttributes>> request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Anchor provider is disabled; sub-account creation skipped.");
            return null;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "sub-accounts")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        ApplyHeaders(httpRequest);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Anchor sub-account creation failed. Status: {Status}, Error: {Err}", response.StatusCode, err);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<AnchorResourceEnvelope<AnchorResource<AnchorSubAccountAttributes>>>(JsonOptions, cancellationToken);
        return result?.Data;
    }

    /// <inheritdoc/>
    public async Task<AnchorResource<AnchorSubAccountAttributes>?> GetSubAccountAsync(
        string subAccountId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Anchor provider is disabled; sub-account fetch skipped.");
            return null;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"sub-accounts/{Uri.EscapeDataString(subAccountId)}");
        ApplyHeaders(httpRequest);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Anchor sub-account fetch failed for {Id}. Status: {Status}", subAccountId, response.StatusCode);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<AnchorResourceEnvelope<AnchorResource<AnchorSubAccountAttributes>>>(JsonOptions, cancellationToken);
        return result?.Data;
    }

    /// <inheritdoc/>
    public async Task<AnchorResource<AnchorTransferAttributes>?> CreateTransferAsync(
        AnchorResourceEnvelope<AnchorResource<AnchorTransferAttributes>> request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Anchor provider is disabled; transfer skipped.");
            return null;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "transfers")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        ApplyHeaders(httpRequest);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Anchor transfer failed. Status: {Status}, Error: {Err}", response.StatusCode, err);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<AnchorResourceEnvelope<AnchorResource<AnchorTransferAttributes>>>(JsonOptions, cancellationToken);
        return result?.Data;
    }
}
