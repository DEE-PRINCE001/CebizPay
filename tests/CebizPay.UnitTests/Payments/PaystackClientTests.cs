using System.Net;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Infrastructure.Payments.Paystack;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="PaystackClient"/> HTTP interactions, error mapping, and result classifications.
/// </summary>
public sealed class PaystackClientTests
{
    private readonly IOptions<PaystackOptions> _options = Microsoft.Extensions.Options.Options.Create(new PaystackOptions
    {
        BaseUrl = "https://api.paystack.co",
        SecretKey = "sk_test_mock_paystack_secret_key_123",
        TimeoutSeconds = 5
    });

    private PaystackClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.paystack.co/")
        };

        return new PaystackClient(httpClient, _options, NullLogger<PaystackClient>.Instance);
    }

    [Fact]
    public async Task ResolveAccountAsync_SuccessfulResponse_ShouldReturnAccountName()
    {
        // Arrange
        var jsonResponse = """
        {
            "status": true,
            "message": "Account number resolved",
            "data": {
                "account_number": "0001234567",
                "account_name": "Jane Doe",
                "bank_id": 9
            }
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var client = CreateClient(handler);

        // Act
        var result = await client.ResolveAccountAsync("058", "0001234567");

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("Jane Doe", result.AccountName);
        Assert.Equal("058", result.BankCode);
        Assert.Equal("0001234567", result.AccountNumber);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ResolveAccountAsync_ErrorResponse_ShouldReturnFailedResult()
    {
        // Arrange
        var jsonResponse = """
        {
            "status": false,
            "message": "Could not resolve account name. Check parameters or try again."
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.UnprocessableEntity, jsonResponse);
        var client = CreateClient(handler);

        // Act
        var result = await client.ResolveAccountAsync("058", "0000000000");

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(result.AccountName);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task CreateRecipientAsync_SuccessfulResponse_ShouldReturnRecipientCode()
    {
        // Arrange
        var jsonResponse = """
        {
            "status": true,
            "message": "Transfer recipient created",
            "data": {
                "recipient_code": "RCP_2x5j67ntnw13vd3",
                "type": "nuban",
                "name": "Jane Doe"
            }
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var client = CreateClient(handler);

        // Act
        var recipientCode = await client.CreateRecipientAsync("Jane Doe", "0001234567", "058", "NGN");

        // Assert
        Assert.Equal("RCP_2x5j67ntnw13vd3", recipientCode);
    }

    [Fact]
    public async Task InitiateTransferAsync_SuccessfulResponse_ShouldReturnSuccessResult()
    {
        // Arrange
        var jsonResponse = """
        {
            "status": true,
            "message": "Transfer has been queued",
            "data": {
                "reference": "CBZPA-PSTK-001",
                "transfer_code": "TRF_2x5j67ntnw13vd3",
                "amount": 500000,
                "currency": "NGN",
                "status": "success",
                "id": 14
            }
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var client = CreateClient(handler);

        // Act
        var result = await client.InitiateTransferAsync(
            recipientCode: "RCP_2x5j67ntnw13vd3",
            amount: 5000m,
            currency: "NGN",
            reference: "CBZPA-PSTK-001",
            narration: "Payout");

        // Assert
        Assert.Equal(PaymentProviderResultStatus.Success, result.Status);
        Assert.Equal("TRF_2x5j67ntnw13vd3", result.ProviderReference);
        Assert.NotNull(result.SafeMetadata);
        Assert.Contains("TRF_2x5j67ntnw13vd3", result.SafeMetadata);
    }

    [Fact]
    public async Task InitiateTransferAsync_BusinessRejection400_ShouldReturnBusinessFailure()
    {
        // Arrange
        var jsonResponse = """
        {
            "status": false,
            "message": "Transfer recipient is inactive or invalid"
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, jsonResponse);
        var client = CreateClient(handler);

        // Act
        var result = await client.InitiateTransferAsync(
            recipientCode: "RCP_INVALID",
            amount: 5000m,
            currency: "NGN",
            reference: "CBZPA-PSTK-002",
            narration: "Payout");

        // Assert
        Assert.Equal(PaymentProviderResultStatus.BusinessFailure, result.Status);
        Assert.Equal("BUSINESS_REJECTION", result.FailureCode);
        Assert.Contains("Transfer recipient is inactive", result.FailureReason);
    }

    [Fact]
    public async Task InitiateTransferAsync_TechnicalError500_ShouldReturnTechnicalFailure()
    {
        // Arrange
        var jsonResponse = """
        {
            "status": false,
            "message": "Gateway internal server error"
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, jsonResponse);
        var client = CreateClient(handler);

        // Act
        var result = await client.InitiateTransferAsync(
            recipientCode: "RCP_123",
            amount: 2000m,
            currency: "NGN",
            reference: "CBZPA-PSTK-003",
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
            "status": true,
            "message": "Transfer verified",
            "data": {
                "transfer_code": "TRF_2x5j67ntnw13vd3",
                "status": "success",
                "amount": 500000
            }
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetTransferStatusAsync("TRF_2x5j67ntnw13vd3");

        // Assert
        Assert.Equal(PaymentProviderResultStatus.Success, result.Status);
        Assert.Equal("TRF_2x5j67ntnw13vd3", result.ProviderReference);
    }

    [Fact]
    public async Task GetTransferStatusAsync_FailedStatus_ShouldReturnBusinessFailure()
    {
        // Arrange
        var jsonResponse = """
        {
            "status": true,
            "message": "Transfer verified",
            "data": {
                "transfer_code": "TRF_2x5j67ntnw13vd3",
                "status": "failed",
                "amount": 500000
            }
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetTransferStatusAsync("TRF_2x5j67ntnw13vd3");

        // Assert
        Assert.Equal(PaymentProviderResultStatus.BusinessFailure, result.Status);
        Assert.Equal("TRANSFER_FAILED", result.FailureCode);
        Assert.Contains("failed", result.FailureReason);
    }

    [Fact]
    public async Task GetTransferStatusAsync_PendingStatus_ShouldReturnUnknown()
    {
        // Arrange
        var jsonResponse = """
        {
            "status": true,
            "message": "Transfer verified",
            "data": {
                "transfer_code": "TRF_2x5j67ntnw13vd3",
                "status": "pending",
                "amount": 500000
            }
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetTransferStatusAsync("TRF_2x5j67ntnw13vd3");

        // Assert
        Assert.Equal(PaymentProviderResultStatus.Unknown, result.Status);
        Assert.Contains("pending", result.FailureReason);
    }
}
