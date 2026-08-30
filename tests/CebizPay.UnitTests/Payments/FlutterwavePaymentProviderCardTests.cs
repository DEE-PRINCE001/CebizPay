using System.Net;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Payments.Flutterwave;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="FlutterwavePaymentProvider"/> card operations.
/// </summary>
public sealed class FlutterwavePaymentProviderCardTests
{
    private readonly IOptions<FlutterwaveOptions> _options = Microsoft.Extensions.Options.Options.Create(new FlutterwaveOptions
    {
        BaseUrl = "https://api.flutterwave.com",
        SecretKey = "FLWSECK_TEST-mock-key-12345"
    });

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private FlutterwavePaymentProvider CreateProvider(HttpMessageHandler handler, ApplicationDbContext dbContext)
    {
        var client = new FlutterwaveClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.flutterwave.com/") }, _options, NullLogger<FlutterwaveClient>.Instance);
        return new FlutterwavePaymentProvider(
            client,
            dbContext,
            NullLogger<FlutterwavePaymentProvider>.Instance);
    }

    [Fact]
    public async Task ChargeSavedCardAsync_DelegatesToClientAndReturnsSuccess()
    {
        using var db = CreateDbContext();
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, """
        {
            "status": "success",
            "message": "Charge completed",
            "data": {
                "id": 888999,
                "tx_ref": "REF-001",
                "flw_ref": "FLW-TOK-888",
                "status": "successful",
                "amount": 1000,
                "currency": "NGN"
            }
        }
        """);

        var provider = CreateProvider(handler, db);
        var request = new CardSavedChargeRequest(
            ProviderToken: "flw_tok_abc",
            Amount: 1000m,
            Currency: Currency.NGN,
            Email: "user@test.com",
            Reference: "REF-001",
            CustomerName: "John Doe");

        var result = await provider.ChargeSavedCardAsync(request);

        Assert.Equal(PaymentProviderResultStatus.Success, result.Status);
        Assert.Equal("FLW-TOK-888", result.ProviderReference);
    }

    [Fact]
    public async Task RefundCardPaymentAsync_DelegatesToClientAndReturnsSuccess()
    {
        using var db = CreateDbContext();
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, """
        {
            "status": "success",
            "message": "Refund processed",
            "data": {
                "id": 112233,
                "flw_ref": "FLW-REF-1122",
                "status": "completed"
            }
        }
        """);

        var provider = CreateProvider(handler, db);
        var request = new CardRefundRequest(
            ProviderTransactionReference: "888999",
            Amount: 1000m,
            Currency: Currency.NGN,
            RefundReference: "CBZRF-001",
            Reason: "Customer requested");

        var result = await provider.RefundCardPaymentAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal("FLW-REF-1122", result.ProviderRefundReference);
    }
}
