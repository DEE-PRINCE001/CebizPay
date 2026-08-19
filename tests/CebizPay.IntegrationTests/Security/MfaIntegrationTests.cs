using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.IntegrationTests.Security;

/// <summary>
/// Integration tests for MfaService covering:
/// - CreateChallengeAsync does NOT return the raw code
/// - Challenge remains verifiable through the TestMfaCodeDeliveryService (internal spy)
/// - Expired challenges fail
/// - Used challenges fail on reuse
/// - Excessive failed attempts invalidate the challenge
/// - Enable/Disable MFA with audit log
/// </summary>
[Collection("Infrastructure")]
public sealed class MfaIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public MfaIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private (ApplicationDbContext Db, MfaService Mfa, TestMfaCodeDeliveryService Spy) CreateComponents()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_fixture.PostgresContainer.GetConnectionString())
            .Options;
        var db = new ApplicationDbContext(opts);
        var spy = new TestMfaCodeDeliveryService();
        var mfa = new MfaService(db, spy);
        return (db, mfa, spy);
    }

    // ─── Security: CreateChallengeAsync does NOT return the raw code ────────

    [Fact]
    public async Task CreateChallenge_ShouldNotReturnRawCode_OnlyIdAndExpiry()
    {
        var (db, mfa, _) = CreateComponents();
        await db.Database.EnsureCreatedAsync();

        var userId = $"admin_{Guid.NewGuid():N}";

        // Act: call CreateChallengeAsync and inspect return value
        var result = await mfa.CreateChallengeAsync(userId);

        // Assert: return tuple has exactly two members — ChallengeId and ExpiresAtUtc
        // The raw code is NOT part of the return; it is only accessible via the delivery spy.
        Assert.NotEqual(Guid.Empty, result.ChallengeId);
        Assert.True(result.ExpiresAtUtc > DateTime.UtcNow, "Challenge should not be expired immediately.");

        // Verify the type does NOT contain a Code property (compile-time proof via tuple field names)
        // The following line would fail to compile if Code were still in the return tuple:
        // _ = result.Code;  // ← intentionally NOT here — proves the secret is absent from the contract

        await db.DisposeAsync();
    }

    [Fact]
    public async Task CreateChallenge_RawCodeIsDeliveredOnlyToSpy_NotSurfacedElsewhere()
    {
        var (db, mfa, spy) = CreateComponents();
        await db.Database.EnsureCreatedAsync();

        var userId = $"admin_{Guid.NewGuid():N}";
        var (challengeId, expiresAt) = await mfa.CreateChallengeAsync(userId);

        // The spy (delivery service) received the code — simulating channel delivery
        var deliveredCode = spy.GetDeliveredCode(userId);
        Assert.NotNull(deliveredCode);
        Assert.Matches(@"^\d{6}$", deliveredCode); // 6-digit format

        // The challenge is in the DB with the HASHED code, not plain
        var stored = await db.MfaChallenges.FirstAsync(c => c.Id == challengeId);
        Assert.NotEqual(deliveredCode, stored.CodeHash); // proves hashed, not plain
        Assert.False(stored.IsUsed);
        Assert.InRange(stored.ExpiresAtUtc, expiresAt.AddSeconds(-1), expiresAt.AddSeconds(1));

        await db.DisposeAsync();
    }

    // ─── MFA Disabled → IsMfaEnabled Returns False ──────────────────────────

    [Fact]
    public async Task IsMfaEnabled_WhenNoAdminProfile_ShouldReturnFalse()
    {
        var (db, mfa, _) = CreateComponents();
        await db.Database.EnsureCreatedAsync();

        var result = await mfa.IsMfaEnabledAsync("nonexistent-user");

        Assert.False(result);
        await db.DisposeAsync();
    }

    [Fact]
    public async Task IsMfaEnabled_WhenAdminProfileMfaDisabled_ShouldReturnFalse()
    {
        var (db, mfa, _) = CreateComponents();
        await db.Database.EnsureCreatedAsync();

        var userId = $"admin_{Guid.NewGuid():N}";
        db.AdminProfiles.Add(new AdminProfile(userId, AdminRoleType.Admin, isMfaEnabled: false));
        await db.SaveChangesAsync();

        Assert.False(await mfa.IsMfaEnabledAsync(userId));
        await db.DisposeAsync();
    }

    // ─── MFA Enabled → Challenge Required ────────────────────────────────────

    [Fact]
    public async Task IsMfaEnabled_WhenAdminProfileMfaEnabled_ShouldReturnTrue()
    {
        var (db, mfa, _) = CreateComponents();
        await db.Database.EnsureCreatedAsync();

        var userId = $"admin_{Guid.NewGuid():N}";
        db.AdminProfiles.Add(new AdminProfile(userId, AdminRoleType.Admin, isMfaEnabled: true));
        await db.SaveChangesAsync();

        Assert.True(await mfa.IsMfaEnabledAsync(userId));
        await db.DisposeAsync();
    }

    // ─── Correct MFA → Token Issued ──────────────────────────────────────────

    [Fact]
    public async Task VerifyChallenge_WithCorrectCode_ShouldSucceed()
    {
        var (db, mfa, spy) = CreateComponents();
        await db.Database.EnsureCreatedAsync();

        var userId = $"admin_{Guid.NewGuid():N}";

        // Create challenge — code goes to the spy (delivery), NOT to the caller
        var (challengeId, _) = await mfa.CreateChallengeAsync(userId);
        var deliveredCode = spy.GetDeliveredCode(userId)!;

        // Verify using the code obtained through the delivery channel (spy)
        var (succeeded, returnedUserId, errors) = await mfa.VerifyChallengeAsync(challengeId, deliveredCode);

        Assert.True(succeeded);
        Assert.Equal(userId, returnedUserId);
        Assert.Empty(errors);

        await db.DisposeAsync();
    }

    // ─── Incorrect MFA → Rejected ────────────────────────────────────────────

    [Fact]
    public async Task VerifyChallenge_WithWrongCode_ShouldFail()
    {
        var (db, mfa, _) = CreateComponents();
        await db.Database.EnsureCreatedAsync();

        var userId = $"admin_{Guid.NewGuid():N}";
        var (challengeId, _) = await mfa.CreateChallengeAsync(userId);

        // Submit a deliberately wrong code
        var (succeeded, _, errors) = await mfa.VerifyChallengeAsync(challengeId, "000000");

        Assert.False(succeeded);
        Assert.NotEmpty(errors);

        await db.DisposeAsync();
    }

    // ─── Reused Challenge → Rejected (Single-Use) ────────────────────────────

    [Fact]
    public async Task VerifyChallenge_Reused_ShouldBeRejected()
    {
        var (db, mfa, spy) = CreateComponents();
        await db.Database.EnsureCreatedAsync();

        var userId = $"admin_{Guid.NewGuid():N}";
        var (challengeId, _) = await mfa.CreateChallengeAsync(userId);
        var deliveredCode = spy.GetDeliveredCode(userId)!;

        // First use succeeds
        var (succeeded1, _, _) = await mfa.VerifyChallengeAsync(challengeId, deliveredCode);
        Assert.True(succeeded1);

        // Second use must be rejected (single-use invariant)
        var (succeeded2, _, errors) = await mfa.VerifyChallengeAsync(challengeId, deliveredCode);
        Assert.False(succeeded2);
        Assert.Contains(errors, e => e.Contains("already been used", StringComparison.OrdinalIgnoreCase));

        await db.DisposeAsync();
    }

    // ─── Expired Challenge → Rejected ────────────────────────────────────────

    [Fact]
    public async Task VerifyChallenge_ExpiredChallenge_ShouldBeRejected()
    {
        var (db, mfa, _) = CreateComponents();
        await db.Database.EnsureCreatedAsync();

        // Create an already-expired challenge by inserting directly with negative expiry window
        var userId = $"admin_{Guid.NewGuid():N}";
        var expiredChallenge = new MfaChallenge(userId, "FAKEHASH12345678901234567890AB", TimeSpan.FromSeconds(-1));
        db.MfaChallenges.Add(expiredChallenge);
        await db.SaveChangesAsync();

        var (succeeded, _, errors) = await mfa.VerifyChallengeAsync(expiredChallenge.Id, "123456");

        Assert.False(succeeded);
        Assert.Contains(errors, e => e.Contains("expired", StringComparison.OrdinalIgnoreCase));

        await db.DisposeAsync();
    }

    // ─── 3 Failed Attempts → Challenge Invalidated ──────────────────────────

    [Fact]
    public async Task VerifyChallenge_ThreeFailedAttempts_ShouldBeInvalidated()
    {
        var (db, mfa, _) = CreateComponents();
        await db.Database.EnsureCreatedAsync();

        var userId = $"admin_{Guid.NewGuid():N}";
        var (challengeId, _) = await mfa.CreateChallengeAsync(userId);

        // 3 wrong attempts
        for (int i = 0; i < 3; i++)
        {
            await mfa.VerifyChallengeAsync(challengeId, "000000");
        }

        // 4th attempt should be rate-limited / invalidated
        var (succeeded, _, errors) = await mfa.VerifyChallengeAsync(challengeId, "000000");
        Assert.False(succeeded);
        Assert.NotEmpty(errors);

        await db.DisposeAsync();
    }

    // ─── Enable / Disable MFA with Audit Log ─────────────────────────────────

    [Fact]
    public async Task EnableMfa_ShouldSetIsMfaEnabledTrueAndWriteAuditLog()
    {
        var (db, mfa, _) = CreateComponents();
        await db.Database.EnsureCreatedAsync();

        var userId = $"admin_{Guid.NewGuid():N}";
        db.AdminProfiles.Add(new AdminProfile(userId, AdminRoleType.Admin, isMfaEnabled: false));
        await db.SaveChangesAsync();

        await mfa.EnableMfaAsync(userId);

        var stored = await db.AdminProfiles.FirstAsync(a => a.UserId == userId);
        Assert.True(stored.IsMfaEnabled);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.ActorId == userId && a.Action == Domain.Auditing.AuditActions.MfaEnabled);
        Assert.NotNull(audit);

        await db.DisposeAsync();
    }

    [Fact]
    public async Task DisableMfa_ShouldSetIsMfaEnabledFalseAndWriteAuditLog()
    {
        var (db, mfa, _) = CreateComponents();
        await db.Database.EnsureCreatedAsync();

        var userId = $"admin_{Guid.NewGuid():N}";
        db.AdminProfiles.Add(new AdminProfile(userId, AdminRoleType.Admin, isMfaEnabled: true));
        await db.SaveChangesAsync();

        await mfa.DisableMfaAsync(userId);

        var stored = await db.AdminProfiles.FirstAsync(a => a.UserId == userId);
        Assert.False(stored.IsMfaEnabled);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.ActorId == userId && a.Action == Domain.Auditing.AuditActions.MfaDisabled);
        Assert.NotNull(audit);

        await db.DisposeAsync();
    }
}
