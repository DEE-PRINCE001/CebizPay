#pragma warning disable CS1591
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Infrastructure.Compliance.Common;
using CebizPay.Infrastructure.Compliance.Dojah;
using CebizPay.Infrastructure.Compliance.Ninja;
using CebizPay.Infrastructure.Compliance.SmileId;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class VerificationRoutingServiceTests
{
    private static VerificationRoutingService CreateService(
        bool dojahEnabled = true,
        bool smileIdEnabled = true,
        bool ninjaEnabled = true)
    {
        return new VerificationRoutingService(
            Options.Create(new DojahOptions { Enabled = dojahEnabled }),
            Options.Create(new SmileIdOptions { Enabled = smileIdEnabled }),
            Options.Create(new NinjaOptions { Enabled = ninjaEnabled }));
    }

    [Theory]
    [InlineData(VerificationCapability.Identity, VerificationProvider.Dojah)]
    [InlineData(VerificationCapability.Biometrics, VerificationProvider.SmileId)]
    [InlineData(VerificationCapability.Document, VerificationProvider.SmileId)]
    [InlineData(VerificationCapability.AmlScreening, VerificationProvider.Dojah)]
    [InlineData(VerificationCapability.Business, VerificationProvider.Dojah)]
    [InlineData(VerificationCapability.BeneficialOwnership, VerificationProvider.Dojah)]
    public void ResolvePrimaryProvider_WhenAllEnabled_ReturnsConfiguredPrimary(
        VerificationCapability capability,
        VerificationProvider expectedProvider)
    {
        var service = CreateService();
        var primary = service.ResolvePrimaryProvider(capability);

        Assert.Equal(expectedProvider, primary);
    }

    [Fact]
    public void ResolvePrimaryProvider_WhenDojahDisabled_FallsBackToSmileIdForIdentity()
    {
        var service = CreateService(dojahEnabled: false, smileIdEnabled: true, ninjaEnabled: true);
        var primary = service.ResolvePrimaryProvider(VerificationCapability.Identity);

        Assert.Equal(VerificationProvider.SmileId, primary);
    }

    [Fact]
    public void GetNextFallbackProvider_IdentityChain_FollowsExactSequence()
    {
        var service = CreateService();

        var fallback1 = service.GetNextFallbackProvider(VerificationCapability.Identity, VerificationProvider.Dojah);
        Assert.Equal(VerificationProvider.SmileId, fallback1);

        var fallback2 = service.GetNextFallbackProvider(VerificationCapability.Identity, VerificationProvider.SmileId);
        Assert.Equal(VerificationProvider.Ninja, fallback2);

        var fallback3 = service.GetNextFallbackProvider(VerificationCapability.Identity, VerificationProvider.Ninja);
        Assert.Null(fallback3);
    }

    [Fact]
    public void GetNextFallbackProvider_BusinessChain_FollowsExactSequence()
    {
        var service = CreateService();

        var fallback1 = service.GetNextFallbackProvider(VerificationCapability.Business, VerificationProvider.Dojah);
        Assert.Equal(VerificationProvider.Ninja, fallback1);

        var fallback2 = service.GetNextFallbackProvider(VerificationCapability.Business, VerificationProvider.Ninja);
        Assert.Equal(VerificationProvider.SmileId, fallback2);

        var fallback3 = service.GetNextFallbackProvider(VerificationCapability.Business, VerificationProvider.SmileId);
        Assert.Null(fallback3);
    }

    [Fact]
    public void GetNextFallbackProvider_WhenMiddleProviderDisabled_SkipsToNextAvailable()
    {
        var service = CreateService(dojahEnabled: true, smileIdEnabled: false, ninjaEnabled: true);

        var fallback = service.GetNextFallbackProvider(VerificationCapability.Identity, VerificationProvider.Dojah);
        Assert.Equal(VerificationProvider.Ninja, fallback);
    }
}
