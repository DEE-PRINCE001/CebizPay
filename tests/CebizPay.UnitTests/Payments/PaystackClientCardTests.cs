using System.Net;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Infrastructure.Payments.Paystack;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for Paystack client card authorization charge, refund, and details verification.
/// </summary>
public sealed class PaystackClientCardTests
{
    private readonly IOptions<PaystackOptions> _options = Microsoft.Extensions.Options.Options.Create(new PaystackOptions
    {
        BaseUrl = "https://api.paystack.co",
        SecretKey = "sk_test_mock_paystack_key_12345",
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
    public async Task ChargeAuthorizationAsync_WhenSuccessful_ReturnsSuccess()
    {
        var jsonResponse = """
        {
            "status": true,
            "message": "Charge attempted",
            "data": {
                "amount": 250000,
                "currency": "NGN",
                "transaction_date": "2026-08-28T10:00:00.000Z",
                "status": "success",
                "reference": "CBZCD-SAVED-PSTK-01",
                "authorization": {
                    "authorization_code": "AUTH_pstk12345",
                    "last4": "4081",
                    "exp_month": "08",
                    "exp_year": "2029",
                    "card_type": "visa",
                    "brand": "visa"
                }
            }
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var client = CreateClient(handler);

        var result = await client.ChargeAuthorizationAsync(
            authorizationCode: "AUTH_pstk12345",
            email: "user@example.com",
            amount: 2500m,
            reference: "CBZCD-SAVED-PSTK-01",
            currency: "NGN");

        Assert.Equal(PaymentProviderResultStatus.Success, result.Status);
        Assert.Equal("CBZCD-SAVED-PSTK-01", result.ProviderReference);
        Assert.NotNull(result.TokenDetails);
        Assert.Equal("4081", result.TokenDetails.Last4);
        Assert.Equal("visa", result.TokenDetails.Brand);
    }

    [Fact]
    public async Task ChargeAuthorizationAsync_WhenDeclined_ReturnsBusinessFailure()
    {
        var jsonResponse = """
        {
            "status": false,
            "message": "Card has expired"
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, jsonResponse);
        var client = CreateClient(handler);

        var result = await client.ChargeAuthorizationAsync(
            authorizationCode: "AUTH_expired_card",
            email: "user@example.com",
            amount: 2500m,
            reference: "CBZCD-SAVED-FAIL",
            currency: "NGN");

        Assert.Equal(PaymentProviderResultStatus.BusinessFailure, result.Status);
        Assert.Equal("Card has expired", result.FailureReason);
    }

    [Fact]
    public async Task RefundTransactionAsync_WhenSuccessful_ReturnsTrue()
    {
        var jsonResponse = """
        {
            "status": true,
            "message": "Refund has been queued",
            "data": {
                "transactionReference": "PSTK-REF-001",
                "status": "processed"
            }
        }
        """;

        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        var client = CreateClient(handler);

        var result = await client.RefundTransactionAsync("PSTK-REF-001", 2500m, "NGN", "Overcharge reversal");

        Assert.True(result.Succeeded);
    }
}
