using System.Net;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Infrastructure.Payments.Flutterwave;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="FlutterwaveClient"/> HTTP interactions, error mapping, and result classifications.
/// </summary>
public sealed class FlutterwaveClientTests
{
    private readonly IOptions<FlutterwaveOptions> _options = Microsoft.Extensions.Options.Options.Create(new FlutterwaveOptions
    {
        BaseUrl = "https://api.flutterwave.com",
        SecretKey = "FLWSECK_TEST-mock-key-12345",
        TimeoutSeconds = 5
    });

    private FlutterwaveClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.flutterwave.com/")
        };

        return new FlutterwaveClient(httpClient, _options, NullLogger<FlutterwaveClient>.Instance);
    }

    [Fact]
    public async Task ResolveAccountAsync_SuccessfulResponse_ShouldReturnAccountName()
    {
        // Arrange
        var jsonResponse = """
        {
            "status": "success",
            "message": "Account details fetched",
            "data": {
                "account_number": "0690000031",
                "account_name": "Pastor Bright"
            }
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var client = CreateClient(handler);

        // Act
        var result = await client.ResolveAccountAsync("044", "0690000031");

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("Pastor Bright", result.AccountName);
        Assert.Equal("044", result.BankCode);
        Assert.Equal("0690000031", result.AccountNumber);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ResolveAccountAsync_ErrorResponse_ShouldReturnFailedResult()
    {
        // Arrange
        var jsonResponse = """
        {
            "status": "error",
            "message": "Sorry, we couldn't resolve this account.",
            "data": null
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, jsonResponse);
        var client = CreateClient(handler);

        // Act
        var result = await client.ResolveAccountAsync("044", "0000000000");

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(result.AccountName);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task InitiateTransferAsync_SuccessfulResponse_ShouldReturnSuccessResult()
    {
        // Arrange
        var jsonResponse = """
        {
            "status": "success",
            "message": "Transfer Queued Successfully",
            "data": {
                "id": 7891011,
                "account_number": "0690000031",
                "bank_code": "044",
                "full_name": "Pastor Bright",
                "currency": "NGN",
                "amount": 5000,
                "fee": 10.75,
                "status": "NEW",
                "reference": "CBZPA-REF-001",
                "bank_name": "ACCESS BANK NIGERIA"
            }
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var client = CreateClient(handler);

        // Act
        var result = await client.InitiateTransferAsync(
            bankCode: "044",
            accountNumber: "0690000031",
            amount: 5000m,
            currency: "NGN",
            reference: "CBZPA-REF-001",
            narration: "Payout");

        // Assert
        Assert.Equal(PaymentProviderResultStatus.Success, result.Status);
        Assert.Equal("7891011", result.ProviderReference);
        Assert.NotNull(result.SafeMetadata);
        Assert.Contains("7891011", result.SafeMetadata);
    }

    [Fact]
    public async Task InitiateTransferAsync_BusinessRejection400_ShouldReturnBusinessFailure()
    {
        // Arrange
        var jsonResponse = """
        {
            "status": "error",
            "message": "Insufficient funds in your Flutterwave balance",
            "data": null
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, jsonResponse);
        var client = CreateClient(handler);

        // Act
        var result = await client.InitiateTransferAsync(
            bankCode: "044",
            accountNumber: "0690000031",
            amount: 50000000m,
            currency: "NGN",
            reference: "CBZPA-REF-002",
            narration: "Payout");

        // Assert
        Assert.Equal(PaymentProviderResultStatus.BusinessFailure, result.Status);
        Assert.Equal("BUSINESS_REJECTION", result.FailureCode);
        Assert.Contains("Insufficient funds", result.FailureReason);
    }

    [Fact]
    public async Task InitiateTransferAsync_TechnicalError500_ShouldReturnTechnicalFailure()
    {
        // Arrange
        var jsonResponse = """
        {
            "status": "error",
            "message": "Internal gateway failure"
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, jsonResponse);
        var client = CreateClient(handler);

        // Act
        var result = await client.InitiateTransferAsync(
            bankCode: "044",
            accountNumber: "0690000031",
            amount: 1000m,
            currency: "NGN",
            reference: "CBZPA-REF-003",
            narration: "Payout");

        // Assert
        Assert.Equal(PaymentProviderResultStatus.TechnicalFailure, result.Status);
        Assert.Equal("HTTP_500", result.FailureCode);
    }

    [Fact]
    public async Task GetTransferStatusAsync_SuccessfulStatus_ShouldReturnSuccess()
    {
        // Arrange
        var jsonResponse = """
        {
            "status": "success",
            "message": "Transfer fetched",
            "data": {
                "id": 7891011,
                "status": "SUCCESSFUL",
                "complete_message": "Transfer completed successfully"
            }
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetTransferStatusAsync("7891011");

        // Assert
        Assert.Equal(PaymentProviderResultStatus.Success, result.Status);
        Assert.Equal("7891011", result.ProviderReference);
    }

    [Fact]
    public async Task GetTransferStatusAsync_FailedStatus_ShouldReturnBusinessFailure()
    {
        // Arrange
        var jsonResponse = """
        {
            "status": "success",
            "message": "Transfer fetched",
            "data": {
                "id": 7891011,
                "status": "FAILED",
                "complete_message": "Destination bank rejected credit"
            }
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetTransferStatusAsync("7891011");

        // Assert
        Assert.Equal(PaymentProviderResultStatus.BusinessFailure, result.Status);
        Assert.Equal("TRANSFER_FAILED", result.FailureCode);
        Assert.Contains("Destination bank rejected", result.FailureReason);
    }

    [Fact]
    public async Task GetTransferStatusAsync_PendingStatus_ShouldReturnUnknown()
    {
        // Arrange
        var jsonResponse = """
        {
            "status": "success",
            "message": "Transfer fetched",
            "data": {
                "id": 7891011,
                "status": "PENDING"
            }
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetTransferStatusAsync("7891011");

        // Assert
        Assert.Equal(PaymentProviderResultStatus.Unknown, result.Status);
        Assert.Contains("PENDING", result.FailureReason);
    }
}
