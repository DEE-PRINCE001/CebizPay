using CebizPay.Domain.Communication.Entities;
using CebizPay.Domain.Communication.Enums;
using Xunit;

namespace CebizPay.UnitTests.Communication;

public class DeviceTokenTests
{
    [Fact]
    public void Create_ValidParameters_InstantiatesActiveDeviceToken()
    {
        var token = DeviceToken.Create("user-1", "fcm-token-abc", DevicePlatform.Android, "Pixel 8");

        Assert.NotEqual(Guid.Empty, token.Id);
        Assert.Equal("user-1", token.UserId);
        Assert.Equal("fcm-token-abc", token.Token);
        Assert.Equal(DevicePlatform.Android, token.Platform);
        Assert.Equal("Pixel 8", token.DeviceModel);
        Assert.True(token.IsActive);
        Assert.Null(token.LastUsedAtUtc);
    }

    [Fact]
    public void Deactivate_ActiveToken_SetsIsActiveToFalse()
    {
        var token = DeviceToken.Create("user-1", "fcm-token-abc", DevicePlatform.iOS);
        var now = DateTime.UtcNow;

        token.Deactivate(now);

        Assert.False(token.IsActive);
        Assert.Equal(now, token.UpdatedAtUtc);
    }

    [Fact]
    public void Activate_InactiveToken_ReactivatesSuccessfully()
    {
        var token = DeviceToken.Create("user-1", "fcm-token-abc", DevicePlatform.Android);
        token.Deactivate(DateTime.UtcNow.AddHours(-1));
        Assert.False(token.IsActive);

        var reactivateTime = DateTime.UtcNow;
        token.Activate("user-2", reactivateTime, "Galaxy S24");

        Assert.True(token.IsActive);
        Assert.Equal("user-2", token.UserId);
        Assert.Equal("Galaxy S24", token.DeviceModel);
        Assert.Equal(reactivateTime, token.UpdatedAtUtc);
    }

    [Fact]
    public void RecordUsed_UpdatesLastUsedAtUtc()
    {
        var token = DeviceToken.Create("user-1", "fcm-token-abc", DevicePlatform.Web);
        var now = DateTime.UtcNow;

        token.RecordUsed(now);

        Assert.Equal(now, token.LastUsedAtUtc);
    }

    [Theory]
    [InlineData("", "token-123")]
    [InlineData("user-1", "")]
    [InlineData("   ", "token-123")]
    [InlineData("user-1", "   ")]
    public void Create_InvalidArguments_ThrowsArgumentException(string userId, string token)
    {
        Assert.Throws<ArgumentException>(() => DeviceToken.Create(userId, token, DevicePlatform.Android));
    }
}
