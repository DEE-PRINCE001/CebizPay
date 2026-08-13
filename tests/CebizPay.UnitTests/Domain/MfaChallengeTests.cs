using CebizPay.Domain.Entities;
using Xunit;

namespace CebizPay.UnitTests.Domain;

/// <summary>
/// Unit tests for MfaChallenge domain entity covering short-lived, single-use, rate-limited behavior.
/// </summary>
public sealed class MfaChallengeTests
{
    private static MfaChallenge CreateChallenge(
        string userId = "user-1",
        string codeHash = "ABCDEF1234567890",
        TimeSpan? window = null)
    {
        return new MfaChallenge(userId, codeHash, window ?? TimeSpan.FromMinutes(5));
    }

    // ─── Creation ────────────────────────────────────────────────────────────

    [Fact]
    public void CreateChallenge_ShouldInitializeWithCorrectDefaults()
    {
        var before = DateTime.UtcNow;
        var challenge = CreateChallenge();
        var after = DateTime.UtcNow;

        Assert.NotEqual(Guid.Empty, challenge.Id);
        Assert.Equal("user-1", challenge.UserId);
        Assert.False(challenge.IsUsed);
        Assert.Equal(0, challenge.FailedAttempts);
        Assert.InRange(challenge.CreatedAtUtc, before, after);
        Assert.InRange(challenge.ExpiresAtUtc, before.AddMinutes(5), after.AddMinutes(5));
    }

    [Fact]
    public void CreateChallenge_WithEmptyUserId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => CreateChallenge(userId: string.Empty));
        Assert.Throws<ArgumentException>(() => CreateChallenge(userId: "   "));
    }

    [Fact]
    public void CreateChallenge_WithEmptyCodeHash_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => CreateChallenge(codeHash: string.Empty));
        Assert.Throws<ArgumentException>(() => CreateChallenge(codeHash: "   "));
    }

    // ─── Expiry (Short-Lived) ─────────────────────────────────────────────

    [Fact]
    public void NewChallenge_ShouldNotBeExpiredImmediately()
    {
        var challenge = CreateChallenge(window: TimeSpan.FromMinutes(5));

        Assert.True(challenge.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public void Challenge_CreatedWithZeroWindow_ShouldBeExpiredImmediately()
    {
        var challenge = CreateChallenge(window: TimeSpan.Zero);

        // ExpiresAtUtc == CreatedAtUtc, which is <= UtcNow
        Assert.True(challenge.ExpiresAtUtc <= DateTime.UtcNow);
    }

    // ─── Single-Use ───────────────────────────────────────────────────────

    [Fact]
    public void MarkUsed_ShouldSetIsUsedToTrue()
    {
        var challenge = CreateChallenge();

        challenge.MarkUsed();

        Assert.True(challenge.IsUsed);
    }

    [Fact]
    public void MarkUsed_CalledTwice_ShouldRemainTrue()
    {
        var challenge = CreateChallenge();

        challenge.MarkUsed();
        challenge.MarkUsed();

        Assert.True(challenge.IsUsed);
    }

    // ─── Rate Limiting ────────────────────────────────────────────────────

    [Fact]
    public void IncrementFailedAttempts_ShouldIncrement()
    {
        var challenge = CreateChallenge();

        challenge.IncrementFailedAttempts();
        Assert.Equal(1, challenge.FailedAttempts);

        challenge.IncrementFailedAttempts();
        Assert.Equal(2, challenge.FailedAttempts);

        challenge.IncrementFailedAttempts();
        Assert.Equal(3, challenge.FailedAttempts);
    }
}
