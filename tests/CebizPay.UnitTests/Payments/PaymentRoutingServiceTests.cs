using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Payments.Common;
using CebizPay.Infrastructure.Payments.Flutterwave;
using CebizPay.Infrastructure.Payments.Monnify;
using CebizPay.Infrastructure.Payments.Paystack;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="PaymentRoutingService"/>.
/// Validates capability-aware provider routing, configuration filtering, and fallback resolution.
/// </summary>
public sealed class PaymentRoutingServiceTests
{
    private static PaymentRoutingService CreateRouter(
        bool flutterwaveEnabled = true,
        bool paystackEnabled = true,
        bool monnifyEnabled = true)
    {
        var flwOptions = Options.Create(new FlutterwaveOptions { Enabled = flutterwaveEnabled });
        var pstkOptions = Options.Create(new PaystackOptions { Enabled = paystackEnabled });
        var mnfyOptions = Options.Create(new MonnifyOptions { Enabled = monnifyEnabled });

        return new PaymentRoutingService(
            flwOptions,
            pstkOptions,
            mnfyOptions,
            NullLogger<PaymentRoutingService>.Instance);
    }

    [Fact]
    public void GetRoute_AllProvidersEnabled_ShouldReturnLockedPriorities()
    {
        // Arrange
        var router = CreateRouter(flutterwaveEnabled: true, paystackEnabled: true, monnifyEnabled: true);

        // Act & Assert
        // Virtual accounts: Monnify
        var vaRoute = router.GetRoute(PaymentCapability.VirtualAccount);
        Assert.Equal(new[] { PaymentProvider.Monnify }, vaRoute);

        // Card funding: Flutterwave -> Paystack
        var cardRoute = router.GetRoute(PaymentCapability.CardFunding);
        Assert.Equal(new[] { PaymentProvider.Flutterwave, PaymentProvider.Paystack }, cardRoute);

        // Bank transfer: Monnify -> Flutterwave -> Paystack
        var bankTransferRoute = router.GetRoute(PaymentCapability.BankTransfer);
        Assert.Equal(new[] { PaymentProvider.Monnify, PaymentProvider.Flutterwave, PaymentProvider.Paystack }, bankTransferRoute);

        // Bank account resolution: Monnify -> Flutterwave -> Paystack
        var resolutionRoute = router.GetRoute(PaymentCapability.BankAccountResolution);
        Assert.Equal(new[] { PaymentProvider.Monnify, PaymentProvider.Flutterwave, PaymentProvider.Paystack }, resolutionRoute);
    }

    [Fact]
    public void BankTransfer_WhenMonnifyDisabled_ShouldRouteFlutterwavePrimaryAndPaystackFallback()
    {
        // Arrange
        var router = CreateRouter(flutterwaveEnabled: true, paystackEnabled: true, monnifyEnabled: false);

        // Act
        var primary = router.ResolvePrimaryProvider(PaymentCapability.BankTransfer);
        var fallback = router.GetNextFallbackProvider(PaymentCapability.BankTransfer, primary);
        var route = router.GetRoute(PaymentCapability.BankTransfer);

        // Assert
        Assert.Equal(PaymentProvider.Flutterwave, primary);
        Assert.Equal(PaymentProvider.Paystack, fallback);
        Assert.Equal(new[] { PaymentProvider.Flutterwave, PaymentProvider.Paystack }, route);
    }

    [Fact]
    public void CardFunding_WhenFlutterwaveDisabled_ShouldRoutePaystackPrimaryAndNoFallback()
    {
        // Arrange
        var router = CreateRouter(flutterwaveEnabled: false, paystackEnabled: true, monnifyEnabled: false);

        // Act
        var primary = router.ResolvePrimaryProvider(PaymentCapability.CardFunding);
        var fallback = router.GetNextFallbackProvider(PaymentCapability.CardFunding, primary);

        // Assert
        Assert.Equal(PaymentProvider.Paystack, primary);
        Assert.Null(fallback);
    }

    [Fact]
    public void ResolvePrimaryProvider_WhenAllDisabled_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var router = CreateRouter(flutterwaveEnabled: false, paystackEnabled: false, monnifyEnabled: false);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            router.ResolvePrimaryProvider(PaymentCapability.BankTransfer));
        Assert.Contains("No enabled payment provider", ex.Message);
    }

    [Theory]
    [InlineData(PaymentProvider.Flutterwave, true, false, false, true)]
    [InlineData(PaymentProvider.Paystack, false, true, false, true)]
    [InlineData(PaymentProvider.Monnify, false, false, true, true)]
    [InlineData(PaymentProvider.Monnify, false, false, false, false)]
    public void IsProviderEnabled_ShouldReflectOptions(
        PaymentProvider provider,
        bool flw,
        bool pstk,
        bool mnfy,
        bool expected)
    {
        // Arrange
        var router = CreateRouter(flw, pstk, mnfy);

        // Act
        var result = router.IsProviderEnabled(provider);

        // Assert
        Assert.Equal(expected, result);
    }
}
