using System.Net;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Infrastructure.Payments.Monnify;
using CebizPay.Infrastructure.Payments.Monnify.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="MonnifyClient"/> disbursement, transfer status query, and bank account resolution.
/// </summary>
public sealed class MonnifyClientTransferTests
{
    private readonly IOptions<MonnifyOptions> _validOptions = Options.Create(new MonnifyOptions
    {
        ApiKey = "MK_TEST_123456",
        SecretKey = "SK_TEST_987654",
        ContractCode = "1234567890",
        SourceAccountNumber = "7820123456",
        BaseUrl = "https://sandbox.monnify.com",
        Enabled = true
    });

    [Fact]
    public async Task InitiateTransferAsync_SuccessfulResponse_ShouldReturnSuccessResult()
    {
        // Arrange
        var authJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifyAuthResponseBody>
        {
            RequestSuccessful = true,
            ResponseBody = new MonnifyAuthResponseBody { AccessToken = "valid_token", ExpiresIn = 3600 }
        });

        var transferResponseJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifySingleTransferResponseBody>
        {
            RequestSuccessful = true,
            ResponseMessage = "success",
            ResponseBody = new MonnifySingleTransferResponseBody
            {
                Reference = "CBZBT-REF-001",
                TransactionReference = "MNFY_DISB_123456",
                Amount = 15000m,
                Currency = "NGN",
                Status = "SUCCESS",
                DestinationAccountName = "Alice Doe",
                DestinationBankCode = "058",
                DestinationAccountNumber = "0123456789",
                Fee = 10.75m
            }
        });

        var handler = new SequentialMockHttpMessageHandler(
            (HttpStatusCode.OK, authJson),
            (HttpStatusCode.OK, transferResponseJson));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sandbox.monnify.com") };
        using var client = new MonnifyClient(httpClient, _validOptions, NullLogger<MonnifyClient>.Instance);

        // Act
        var result = await client.InitiateTransferAsync(
            destinationBankCode: "058",
            destinationAccountNumber: "0123456789",
            amount: 15000m,
            currency: "NGN",
            reference: "CBZBT-REF-001",
            narration: "Test Payout");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PaymentProviderResultStatus.Success, result.Status);
        Assert.Equal("MNFY_DISB_123456", result.ProviderReference);
        Assert.NotNull(result.SafeMetadata);
        Assert.Contains("058", result.SafeMetadata);
    }

    [Fact]
    public async Task InitiateTransferAsync_BusinessRejection_ShouldReturnBusinessFailure()
    {
        // Arrange
        var authJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifyAuthResponseBody>
        {
            RequestSuccessful = true,
            ResponseBody = new MonnifyAuthResponseBody { AccessToken = "valid_token", ExpiresIn = 3600 }
        });

        var transferResponseJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifySingleTransferResponseBody>
        {
            RequestSuccessful = false,
            ResponseCode = "INVALID_ACCOUNT",
            ResponseMessage = "Destination account number is invalid or inactive.",
            ResponseBody = null
        });

        var handler = new SequentialMockHttpMessageHandler(
            (HttpStatusCode.OK, authJson),
            (HttpStatusCode.BadRequest, transferResponseJson));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sandbox.monnify.com") };
        using var client = new MonnifyClient(httpClient, _validOptions, NullLogger<MonnifyClient>.Instance);

        // Act
        var result = await client.InitiateTransferAsync(
            destinationBankCode: "058",
            destinationAccountNumber: "0000000000",
            amount: 5000m,
            currency: "NGN",
            reference: "CBZBT-REF-002",
            narration: "Test Payout");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PaymentProviderResultStatus.BusinessFailure, result.Status);
        Assert.Equal("INVALID_ACCOUNT", result.FailureCode);
        Assert.Contains("Destination account number is invalid", result.FailureReason);
    }

    [Fact]
    public async Task InitiateTransferAsync_Server500Error_ShouldReturnTechnicalFailure()
    {
        // Arrange
        var authJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifyAuthResponseBody>
        {
            RequestSuccessful = true,
            ResponseBody = new MonnifyAuthResponseBody { AccessToken = "valid_token", ExpiresIn = 3600 }
        });

        var handler = new SequentialMockHttpMessageHandler(
            (HttpStatusCode.OK, authJson),
            (HttpStatusCode.InternalServerError, "{\"requestSuccessful\":false,\"responseMessage\":\"Internal Server Error\"}"));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sandbox.monnify.com") };
        using var client = new MonnifyClient(httpClient, _validOptions, NullLogger<MonnifyClient>.Instance);

        // Act
        var result = await client.InitiateTransferAsync(
            destinationBankCode: "058",
            destinationAccountNumber: "0123456789",
            amount: 5000m,
            currency: "NGN",
            reference: "CBZBT-REF-003",
            narration: "Test Payout");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PaymentProviderResultStatus.TechnicalFailure, result.Status);
        Assert.Equal("HTTP_500", result.FailureCode);
    }

    [Fact]
    public async Task GetTransferStatusAsync_SuccessfulStatus_ShouldReturnSuccess()
    {
        // Arrange
        var authJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifyAuthResponseBody>
        {
            RequestSuccessful = true,
            ResponseBody = new MonnifyAuthResponseBody { AccessToken = "valid_token", ExpiresIn = 3600 }
        });

        var summaryJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifyDisbursementSummaryResponseBody>
        {
            RequestSuccessful = true,
            ResponseBody = new MonnifyDisbursementSummaryResponseBody
            {
                Reference = "CBZBT-REF-004",
                TransactionReference = "MNFY_TX_SUMMARY_001",
                Amount = 10000m,
                Status = "SUCCESS",
                DestinationAccountName = "Alice Doe"
            }
        });

        var handler = new SequentialMockHttpMessageHandler(
            (HttpStatusCode.OK, authJson),
            (HttpStatusCode.OK, summaryJson));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sandbox.monnify.com") };
        using var client = new MonnifyClient(httpClient, _validOptions, NullLogger<MonnifyClient>.Instance);

        // Act
        var result = await client.GetTransferStatusAsync("CBZBT-REF-004");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PaymentProviderResultStatus.Success, result.Status);
        Assert.Equal("MNFY_TX_SUMMARY_001", result.ProviderReference);
    }

    [Fact]
    public async Task ResolveAccountAsync_ValidAccount_ShouldReturnBeneficiaryName()
    {
        // Arrange
        var authJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifyAuthResponseBody>
        {
            RequestSuccessful = true,
            ResponseBody = new MonnifyAuthResponseBody { AccessToken = "valid_token", ExpiresIn = 3600 }
        });

        var validateJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifyAccountValidationResponseBody>
        {
            RequestSuccessful = true,
            ResponseBody = new MonnifyAccountValidationResponseBody
            {
                AccountNumber = "0123456789",
                AccountName = "ALICE CHUKWU",
                BankCode = "058"
            }
        });

        var handler = new SequentialMockHttpMessageHandler(
            (HttpStatusCode.OK, authJson),
            (HttpStatusCode.OK, validateJson));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sandbox.monnify.com") };
        using var client = new MonnifyClient(httpClient, _validOptions, NullLogger<MonnifyClient>.Instance);

        // Act
        var result = await client.ResolveAccountAsync("058", "0123456789");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Succeeded);
        Assert.Equal("ALICE CHUKWU", result.AccountName);
        Assert.Equal("058", result.BankCode);
        Assert.Equal("0123456789", result.AccountNumber);
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
