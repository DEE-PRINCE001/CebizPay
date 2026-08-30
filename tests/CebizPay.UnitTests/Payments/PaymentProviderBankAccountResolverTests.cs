using System.Net;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Payments.Common;
using CebizPay.Infrastructure.Payments.Flutterwave;
using CebizPay.Infrastructure.Payments.Monnify;
using CebizPay.Infrastructure.Payments.Paystack;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="PaymentProviderBankAccountResolver"/> testing format validation,
/// capability-based provider routing (Monnify -> Flutterwave -> Paystack), and fallback resilience.
/// </summary>
public sealed class PaymentProviderBankAccountResolverTests
{
    private readonly IMonnifyClient _mockMonnifyClient = Substitute.For<IMonnifyClient>();
    private readonly IPaymentRoutingService _routingService = new PaymentRoutingService();

    private PaymentProviderBankAccountResolver CreateResolver(
        MockHttpMessageHandler flwHandler,
        MockHttpMessageHandler pstkHandler)
    {
        var flwOptions = Options.Create(new FlutterwaveOptions
        {
            SecretKey = "FLWSECK_TEST",
            BaseUrl = "https://api.flutterwave.com",
            Enabled = true
        });
        var flwHttpClient = new HttpClient(flwHandler) { BaseAddress = new Uri("https://api.flutterwave.com") };
        var flwClient = new FlutterwaveClient(flwHttpClient, flwOptions, NullLogger<FlutterwaveClient>.Instance);

        var pstkOptions = Options.Create(new PaystackOptions
        {
            SecretKey = "sk_test_12345",
            BaseUrl = "https://api.paystack.co",
            Enabled = true
        });
        var pstkHttpClient = new HttpClient(pstkHandler) { BaseAddress = new Uri("https://api.paystack.co") };
        var pstkClient = new PaystackClient(pstkHttpClient, pstkOptions, NullLogger<PaystackClient>.Instance);

        return new PaymentProviderBankAccountResolver(
            _mockMonnifyClient,
            flwClient,
            pstkClient,
            _routingService,
            NullLogger<PaymentProviderBankAccountResolver>.Instance);
    }

    [Theory]
    [InlineData("", "0123456789", "Bank code is required")]
    [InlineData("058", "", "10-digit numeric")]
    [InlineData("058", "12345", "10-digit numeric")]
    [InlineData("058", "12345678901", "10-digit numeric")]
    [InlineData("058", "012345678A", "10-digit numeric")]
    public async Task ResolveAsync_InvalidInputs_ShouldFailFastWithoutProviderCall(
        string bankCode,
        string accountNumber,
        string expectedErrorSnippet)
    {
        // Arrange
        var resolver = CreateResolver(
            new MockHttpMessageHandler(HttpStatusCode.OK, "{}"),
            new MockHttpMessageHandler(HttpStatusCode.OK, "{}"));

        // Act
        var result = await resolver.ResolveAsync(bankCode, accountNumber);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Succeeded);
        Assert.Contains(expectedErrorSnippet, result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        await _mockMonnifyClient.DidNotReceive().ResolveAccountAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_MonnifyPrimarySucceeds_ShouldReturnAccountDetails()
    {
        // Arrange
        _mockMonnifyClient.ResolveAccountAsync("058", "0123456789", Arg.Any<CancellationToken>())
            .Returns(new BankAccountResolutionResult(
                Succeeded: true,
                AccountName: "ALICE MONNIFY",
                BankCode: "058",
                AccountNumber: "0123456789"));

        var resolver = CreateResolver(
            new MockHttpMessageHandler(HttpStatusCode.OK, "{}"),
            new MockHttpMessageHandler(HttpStatusCode.OK, "{}"));

        // Act
        var result = await resolver.ResolveAsync("058", "0123456789");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Succeeded);
        Assert.Equal("ALICE MONNIFY", result.AccountName);
        Assert.Equal("058", result.BankCode);
        Assert.Equal("0123456789", result.AccountNumber);
    }

    [Fact]
    public async Task ResolveAsync_MonnifyFails_FlutterwaveFallbackSucceeds_ShouldReturnFlutterwaveDetails()
    {
        // Arrange
        _mockMonnifyClient.ResolveAccountAsync("044", "1234567890", Arg.Any<CancellationToken>())
            .Returns(new BankAccountResolutionResult(false, null, "044", "1234567890", "Monnify failed"));

        var flwResponseJson = @"{
            ""status"": ""success"",
            ""message"": ""Account details fetched"",
            ""data"": {
                ""account_number"": ""1234567890"",
                ""account_name"": ""BOB FLUTTERWAVE""
            }
        }";

        var resolver = CreateResolver(
            new MockHttpMessageHandler(HttpStatusCode.OK, flwResponseJson),
            new MockHttpMessageHandler(HttpStatusCode.OK, "{}"));

        // Act
        var result = await resolver.ResolveAsync("044", "1234567890");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Succeeded);
        Assert.Equal("BOB FLUTTERWAVE", result.AccountName);
    }
}
