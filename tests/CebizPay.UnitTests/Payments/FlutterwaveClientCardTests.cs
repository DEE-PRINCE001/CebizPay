using System.Net;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Infrastructure.Payments.Flutterwave;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for Flutterwave client card tokenization charge, refund, and details verification.
/// </summary>
public sealed class FlutterwaveClientCardTests
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
    public async Task ChargeTokenizedCardAsync_WhenSuccessful_ReturnsSuccessfulResult()
    {
        var jsonResponse = """
        {
            "status": "success",
            "message": "Charge initiated",
            "data": {
                "id": 288200,
                "tx_ref": "CBZCD-REF-1001",
                "flw_ref": "FLW-MOCK-99281",
                "amount": 5000,
                "currency": "NGN",
                "status": "successful",
                "card": {
                    "last_4digits": "4242",
                    "type": "VISA",
                    "expiry": "12/30"
                }
            }
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var client = CreateClient(handler);

        var result = await client.ChargeTokenizedCardAsync(
            token: "flw_card_tok_123",
            amount: 5000m,
            currency: "NGN",
            email: "user@example.com",
            txRef: "CBZCD-REF-1001");

        Assert.Equal(PaymentProviderResultStatus.Success, result.Status);
        Assert.Equal("FLW-MOCK-99281", result.ProviderReference);
        Assert.NotNull(result.TokenDetails);
        Assert.Equal("4242", result.TokenDetails.Last4);
        Assert.Equal("VISA", result.TokenDetails.Brand);
    }

    [Fact]
    public async Task ChargeTokenizedCardAsync_WhenDeclined_ReturnsBusinessFailure()
    {
        var jsonResponse = """
        {
            "status": "error",
            "message": "Card declined: Insufficient funds",
            "data": null
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, jsonResponse);
        var client = CreateClient(handler);

        var result = await client.ChargeTokenizedCardAsync(
            token: "flw_card_tok_insufficient",
            amount: 100000m,
            currency: "NGN",
            email: "user@example.com",
            txRef: "CBZCD-REF-1002");

        Assert.Equal(PaymentProviderResultStatus.BusinessFailure, result.Status);
        Assert.Contains("Card declined", result.FailureReason);
    }

    [Fact]
    public async Task RefundTransactionAsync_WhenSuccessful_ReturnsTrue()
    {
        var jsonResponse = """
        {
            "status": "success",
            "message": "Refund processed",
            "data": {
                "id": 98765,
                "flw_ref": "FLW-REFUND-001",
                "status": "completed"
            }
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var client = CreateClient(handler);

        var result = await client.RefundTransactionAsync("288200", 5000m, "Customer requested refund");

        Assert.True(result.Succeeded);
        Assert.Equal("FLW-REFUND-001", result.ProviderRefundReference);
    }

    [Fact]
    public async Task VerifyTransactionWithDetailsAsync_ReturnsCardDetails()
    {
        var jsonResponse = """
        {
            "status": "success",
            "message": "Transaction fetched",
            "data": {
                "id": 334455,
                "tx_ref": "CBZVR-REF-001",
                "flw_ref": "FLW-TOK-CARD-99",
                "amount": 50,
                "currency": "NGN",
                "status": "successful",
                "card": {
                    "last_4digits": "1111",
                    "type": "MASTERCARD",
                    "expiry": "05/28"
                }
            }
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var client = CreateClient(handler);

        var (providerResult, tokenDetails) = await client.VerifyTransactionWithDetailsAsync("334455");

        Assert.Equal(PaymentProviderResultStatus.Success, providerResult.Status);
        Assert.NotNull(tokenDetails);
        Assert.Equal("1111", tokenDetails.Last4);
        Assert.Equal("MASTERCARD", tokenDetails.Brand);
        Assert.Equal("FLW-TOK-CARD-99", tokenDetails.Token);
    }
}
