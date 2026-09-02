using System.Security.Cryptography;
using System.Text;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using Xunit;

namespace CebizPay.UnitTests.Domain;

/// <summary>
/// Domain unit tests for AdminInvitation aggregate.
/// Covers cryptographic token hash storage, 24-hour lifecycle, expiration, single-use redemption, and cancellation.
/// </summary>
public sealed class AdminInvitationTests
{
    private static string HashToken(string rawToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
    }

    [Fact]
    public void CreateInvitation_ShouldSetPendingStatusAnd24HourExpiry()
    {
        var rawToken = "test-raw-token-1234567890";
        var tokenHash = HashToken(rawToken);
        var email = "NEWADMIN@cebizpay.com";

        var invitation = new AdminInvitation(email, AdminRoleType.Admin, tokenHash, "super-admin-user-id");

        Assert.NotEqual(Guid.Empty, invitation.Id);
        Assert.Equal("newadmin@cebizpay.com", invitation.Email);
        Assert.Equal(AdminRoleType.Admin, invitation.Role);
        Assert.Equal(tokenHash, invitation.TokenHash);
        Assert.Equal(AdminInvitationStatus.Pending, invitation.Status);
        Assert.Equal("super-admin-user-id", invitation.InvitedByUserId);
        Assert.Null(invitation.RedeemedByUserId);
        Assert.Null(invitation.RedeemedAtUtc);

        var expectedExpiry = invitation.CreatedAtUtc.AddHours(24);
        Assert.True((invitation.ExpiresAtUtc - expectedExpiry).Duration() < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void CreateInvitation_WithEmptyArguments_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new AdminInvitation("", AdminRoleType.Admin, "hash", "superadmin"));
        Assert.Throws<ArgumentException>(() => new AdminInvitation("admin@test.com", AdminRoleType.Admin, "", "superadmin"));
        Assert.Throws<ArgumentException>(() => new AdminInvitation("admin@test.com", AdminRoleType.Admin, "hash", ""));
    }

    [Fact]
    public void Redeem_WhenPendingAndUnexpired_ShouldSucceed()
    {
        var invitation = new AdminInvitation("admin@test.com", AdminRoleType.Admin, "hash", "superadmin");
        var redemptionTime = DateTime.UtcNow.AddHours(1);

        invitation.Redeem("new-user-id", redemptionTime);

        Assert.Equal(AdminInvitationStatus.Redeemed, invitation.Status);
        Assert.Equal("new-user-id", invitation.RedeemedByUserId);
        Assert.Equal(redemptionTime, invitation.RedeemedAtUtc);
    }

    [Fact]
    public void Redeem_WhenExpired_ShouldMarkExpiredAndThrow()
    {
        var invitation = new AdminInvitation("admin@test.com", AdminRoleType.Admin, "hash", "superadmin");
        var expiredTime = DateTime.UtcNow.AddHours(25);

        var ex = Assert.Throws<InvalidOperationException>(() => invitation.Redeem("new-user-id", expiredTime));
        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(AdminInvitationStatus.Expired, invitation.Status);
    }

    [Fact]
    public void Redeem_WhenAlreadyRedeemed_ShouldThrow()
    {
        var invitation = new AdminInvitation("admin@test.com", AdminRoleType.Admin, "hash", "superadmin");
        invitation.Redeem("user-1", DateTime.UtcNow);

        var ex = Assert.Throws<InvalidOperationException>(() => invitation.Redeem("user-2", DateTime.UtcNow));
        Assert.Contains("Cannot redeem invitation with status", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cancel_WhenPending_ShouldTransitionToCancelled()
    {
        var invitation = new AdminInvitation("admin@test.com", AdminRoleType.Admin, "hash", "superadmin");

        invitation.Cancel(DateTime.UtcNow);

        Assert.Equal(AdminInvitationStatus.Cancelled, invitation.Status);
    }

    [Fact]
    public void Cancel_WhenAlreadyRedeemed_ShouldThrow()
    {
        var invitation = new AdminInvitation("admin@test.com", AdminRoleType.Admin, "hash", "superadmin");
        invitation.Redeem("user-1", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => invitation.Cancel(DateTime.UtcNow));
    }

    [Fact]
    public void IsExpired_ShouldReturnTrueWhenPastExpiryOrNotPending()
    {
        var invitation = new AdminInvitation("admin@test.com", AdminRoleType.Admin, "hash", "superadmin");

        Assert.False(invitation.IsExpired(DateTime.UtcNow.AddHours(10)));
        Assert.True(invitation.IsExpired(DateTime.UtcNow.AddHours(25)));

        invitation.Cancel(DateTime.UtcNow);
        Assert.True(invitation.IsExpired(DateTime.UtcNow));
    }
}
