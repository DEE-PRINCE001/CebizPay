using System.Net;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Payments.Paystack;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="PaystackPaymentProvider"/> card operations.
/// </summary>
public sealed class PaystackPaymentProviderCardTests
{
    private readonly IOptions<PaystackOptions> _options = Microsoft.Extensions.Options.Options.Create(new PaystackOptions
    {
        BaseUrl = "https://api.paystack.co",
        SecretKey = "sk_test_key_12345"
    });

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private PaystackPaymentProvider CreateProvider(HttpMessageHandler handler, ApplicationDbContext dbContext)
    {
        var client = new PaystackClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.paystack.co/") }, _options, NullLogger<PaystackClient>.Instance);
        return new PaystackPaymentProvider(
            client,
            dbContext,
            NullLogger<PaystackPaymentProvider>.Instance);
    }

    [Fact]
    public async Task ChargeSavedCardAsync_DelegatesToClientAndReturnsSuccess()
    {
        using var db = CreateDbContext();
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, """
        {
            "status": true,
            "message": "Charge successful",
            "data": {
                "reference": "REF-002",
                "status": "success",
                "amount": 350000,
                "currency": "NGN"
            }
        }
        """);

        var provider = CreateProvider(handler, db);
        var request = new CardSavedChargeRequest(
            ProviderToken: "AUTH_code_123",
            Amount: 3500m,
            Currency: Currency.NGN,
            Email: "user@test.com",
            Reference: "REF-002");

        var result = await provider.ChargeSavedCardAsync(request);

        Assert.Equal(PaymentProviderResultStatus.Success, result.Status);
        Assert.Equal("REF-002", result.ProviderReference);
    }

    [Fact]
    public async Task RefundCardPaymentAsync_DelegatesToClientAndReturnsSuccess()
    {
        using var db = CreateDbContext();
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, """
        {
            "status": true,
            "message": "Refund processed",
            "data": {
                "transactionReference": "pstk_ref_002",
                "status": "processed"
            }
        }
        """);

        var provider = CreateProvider(handler, db);
        var request = new CardRefundRequest(
            ProviderTransactionReference: "pstk_tx_002",
            Amount: 3500m,
            Currency: Currency.NGN,
            RefundReference: "CBZRF-002",
            Reason: "Customer requested reversal");

        var result = await provider.RefundCardPaymentAsync(request);

        Assert.True(result.Succeeded);
    }
}
