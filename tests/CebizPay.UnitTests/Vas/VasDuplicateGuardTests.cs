using CebizPay.Domain.Vas.Enums;
using CebizPay.Infrastructure.Vas;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CebizPay.UnitTests.Vas;

public class VasDuplicateGuardTests
{
    private readonly VasDuplicateGuard _guard = new(NullLogger<VasDuplicateGuard>.Instance, connectionMultiplexer: null);

    [Fact]
    public async Task TryAcquireDuplicateLockAsync_FirstAttempt_Succeeds()
    {
        var result = await _guard.TryAcquireDuplicateLockAsync(
            VasType.Airtime,
            "08031234567",
            1000m,
            VasNetwork.Mtn);

        Assert.True(result);
    }

    [Fact]
    public async Task TryAcquireDuplicateLockAsync_SameParametersWithin120Seconds_Fails()
    {
        var phone = "08039998877";
        var amount = 1500m;

        var first = await _guard.TryAcquireDuplicateLockAsync(VasType.Airtime, phone, amount, VasNetwork.Mtn);
        Assert.True(first);

        var second = await _guard.TryAcquireDuplicateLockAsync(VasType.Airtime, phone, amount, VasNetwork.Mtn);
        Assert.False(second);
    }

    [Fact]
    public async Task TryAcquireDuplicateLockAsync_DifferentAmount_Succeeds()
    {
        var phone = "08035554433";

        var first = await _guard.TryAcquireDuplicateLockAsync(VasType.Airtime, phone, 500m, VasNetwork.Mtn);
        Assert.True(first);

        var second = await _guard.TryAcquireDuplicateLockAsync(VasType.Airtime, phone, 1000m, VasNetwork.Mtn);
        Assert.True(second);
    }

    [Fact]
    public async Task ReleaseDuplicateLockAsync_AllowsImmediateReacquisition()
    {
        var phone = "08031112233";
        var amount = 2000m;

        var first = await _guard.TryAcquireDuplicateLockAsync(VasType.Airtime, phone, amount, VasNetwork.Mtn);
        Assert.True(first);

        await _guard.ReleaseDuplicateLockAsync(VasType.Airtime, phone, amount, VasNetwork.Mtn);

        var second = await _guard.TryAcquireDuplicateLockAsync(VasType.Airtime, phone, amount, VasNetwork.Mtn);
        Assert.True(second);
    }
}
