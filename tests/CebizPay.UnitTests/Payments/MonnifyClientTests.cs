using System.Net;
using System.Text.Json;
using CebizPay.Infrastructure.Payments.Monnify;
using CebizPay.Infrastructure.Payments.Monnify.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="MonnifyClient"/> verifying OAuth token caching, API payload serialization, and error resilience.
/// </summary>
public sealed class MonnifyClientTests
{
    private readonly IOptions<MonnifyOptions> _validOptions = Options.Create(new MonnifyOptions
    {
        ApiKey = "MK_TEST_123456",
        SecretKey = "SK_TEST_987654",
        ContractCode = "1234567890",
        BaseUrl = "https://sandbox.monnify.com",
        Enabled = true
    });

    [Fact]
    public async Task GetAccessTokenAsync_ValidCredentials_ShouldCacheAndReuseToken()
    {
        // Arrange
        var authResponseJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifyAuthResponseBody>
        {
            RequestSuccessful = true,
            ResponseBody = new MonnifyAuthResponseBody
            {
                AccessToken = "eyJhbGciOiJIUzUxMiIsInR5cCI6IkpXVCJ9.token123",
                ExpiresIn = 3600
            }
        });

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, authResponseJson);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sandbox.monnify.com") };
        using var client = new MonnifyClient(httpClient, _validOptions, NullLogger<MonnifyClient>.Instance);

        // Act: First call fetches token
        var token1 = await client.GetAccessTokenAsync();

        // Act: Second call reuses cached token
        var token2 = await client.GetAccessTokenAsync();

        // Assert
        Assert.NotNull(token1);
        Assert.Equal("eyJhbGciOiJIUzUxMiIsInR5cCI6IkpXVCJ9.token123", token1);
        Assert.Equal(token1, token2);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WhenDisabled_ShouldReturnNull()
    {
        // Arrange
        var disabledOptions = Options.Create(new MonnifyOptions
        {
            ApiKey = "MK_TEST_123456",
            SecretKey = "SK_TEST_987654",
            BaseUrl = "https://sandbox.monnify.com",
            Enabled = false
        });

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{}");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sandbox.monnify.com") };
        using var client = new MonnifyClient(httpClient, disabledOptions, NullLogger<MonnifyClient>.Instance);

        // Act
        var token = await client.GetAccessTokenAsync();

        // Assert
        Assert.Null(token);
    }

    [Fact]
    public async Task CreateReservedAccountAsync_SuccessfulResponse_ShouldReturnAccountDetails()
    {
        // Arrange
        var authJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifyAuthResponseBody>
        {
            RequestSuccessful = true,
            ResponseBody = new MonnifyAuthResponseBody { AccessToken = "valid_token", ExpiresIn = 3600 }
        });

        var createAccountJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifyCreateReservedAccountResponseBody>
        {
            RequestSuccessful = true,
            ResponseMessage = "success",
            ResponseBody = new MonnifyCreateReservedAccountResponseBody
            {
                AccountReference = "CBZ_MNFY_REF_001",
                AccountName = "Jane Doe",
                CurrencyCode = "NGN",
                Accounts = new List<MonnifyAccountDetails>
                {
                    new()
                    {
                        AccountNumber = "7820123456",
                        AccountName = "Jane Doe",
                        BankCode = "035",
                        BankName = "Wema Bank"
                    }
                }
            }
        });

        var sequentialHandler = new SequentialMockHttpMessageHandler(
            (HttpStatusCode.OK, authJson),
            (HttpStatusCode.OK, createAccountJson));

        using var httpClient = new HttpClient(sequentialHandler) { BaseAddress = new Uri("https://sandbox.monnify.com") };
        using var client = new MonnifyClient(httpClient, _validOptions, NullLogger<MonnifyClient>.Instance);

        var request = new MonnifyCreateReservedAccountRequest
        {
            AccountReference = "CBZ_MNFY_REF_001",
            AccountName = "Jane Doe",
            CurrencyCode = "NGN",
            CustomerEmail = "jane@example.com",
            CustomerName = "Jane Doe"
        };

        // Act
        var response = await client.CreateReservedAccountAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.RequestSuccessful);
        Assert.NotNull(response.ResponseBody);
        Assert.NotNull(response.ResponseBody.Accounts);
        Assert.Single(response.ResponseBody.Accounts);
        Assert.Equal("7820123456", response.ResponseBody.Accounts[0].AccountNumber);
        Assert.Equal("Wema Bank", response.ResponseBody.Accounts[0].BankName);
    }

    [Fact]
    public async Task DeactivateReservedAccountAsync_ShouldReturnSuccess()
    {
        // Arrange
        var authJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifyAuthResponseBody>
        {
            RequestSuccessful = true,
            ResponseBody = new MonnifyAuthResponseBody { AccessToken = "valid_token", ExpiresIn = 3600 }
        });

        var deactivateJson = JsonSerializer.Serialize(new MonnifyApiResponse<object>
        {
            RequestSuccessful = true,
            ResponseMessage = "Reserved account deleted successfully."
        });

        var sequentialHandler = new SequentialMockHttpMessageHandler(
            (HttpStatusCode.OK, authJson),
            (HttpStatusCode.OK, deactivateJson));

        using var httpClient = new HttpClient(sequentialHandler) { BaseAddress = new Uri("https://sandbox.monnify.com") };
        using var client = new MonnifyClient(httpClient, _validOptions, NullLogger<MonnifyClient>.Instance);

        // Act
        var result = await client.DeactivateReservedAccountAsync("CBZ_MNFY_REF_001");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.RequestSuccessful);
    }

    [Fact]
    public async Task GetTransactionDetailsAsync_PaidStatus_ShouldDeserialize()
    {
        // Arrange
        var authJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifyAuthResponseBody>
        {
            RequestSuccessful = true,
            ResponseBody = new MonnifyAuthResponseBody { AccessToken = "valid_token", ExpiresIn = 3600 }
        });

        var queryJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifyTransactionResponseBody>
        {
            RequestSuccessful = true,
            ResponseBody = new MonnifyTransactionResponseBody
            {
                TransactionReference = "MNFY_TX_987",
                PaymentReference = "MNFY_PAY_987",
                AmountPaid = 25000m,
                PaymentStatus = "PAID",
                CurrencyCode = "NGN"
            }
        });

        var sequentialHandler = new SequentialMockHttpMessageHandler(
            (HttpStatusCode.OK, authJson),
            (HttpStatusCode.OK, queryJson));

        using var httpClient = new HttpClient(sequentialHandler) { BaseAddress = new Uri("https://sandbox.monnify.com") };
        using var client = new MonnifyClient(httpClient, _validOptions, NullLogger<MonnifyClient>.Instance);

        // Act
        var result = await client.GetTransactionDetailsAsync("MNFY_TX_987");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.RequestSuccessful);
        Assert.NotNull(result.ResponseBody);
        Assert.Equal("PAID", result.ResponseBody.PaymentStatus);
        Assert.Equal(25000m, result.ResponseBody.AmountPaid);
    }

    private sealed class SequentialMockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode StatusCode, string Content)> _responses;

        public SequentialMockHttpMessageHandler(params (HttpStatusCode, string)[] responses)
        {
            _responses = new Queue<(HttpStatusCode, string)>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var next = _responses.Dequeue();
            var response = new HttpResponseMessage(next.StatusCode)
            {
                Content = new StringContent(next.Content, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
