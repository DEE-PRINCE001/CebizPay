using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Utils;
using CebizPay.Infrastructure.Identity;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.IntegrationTests.Security;

/// <summary>
/// Authoritative PostgreSQL concurrency integration tests proving that simultaneous registration
/// attempts claiming the same canonical phone number under varying textual representations
/// result in exactly one successful claim, zero duplicates, and zero database corruption.
/// </summary>
[Collection("Infrastructure")]
public sealed class PhoneIdentityConcurrencyTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public PhoneIdentityConcurrencyTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private ApplicationDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_fixture.PostgresContainer.GetConnectionString())
            .Options;
        return new ApplicationDbContext(opts);
    }

    private (IdentityService Service, ApplicationDbContext Context) CreateIdentityService()
    {
        var db = CreateContext();
        var userStore = new UserStore<ApplicationUser, IdentityRole, ApplicationDbContext, string>(db);
        var identityOptions = Microsoft.Extensions.Options.Options.Create(new IdentityOptions
        {
            User = { RequireUniqueEmail = true }
        });

        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var userValidators = new List<IUserValidator<ApplicationUser>> { new UserValidator<ApplicationUser>() };
        var passwordValidators = new List<IPasswordValidator<ApplicationUser>> { new PasswordValidator<ApplicationUser>() };

        var userManager = new UserManager<ApplicationUser>(
            userStore,
            identityOptions,
            passwordHasher,
            userValidators,
            passwordValidators,
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            NullLogger<UserManager<ApplicationUser>>.Instance);

        var contextAccessor = Substitute.For<IHttpContextAccessor>();
        var userPrincipalFactory = Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var signInManager = Substitute.For<SignInManager<ApplicationUser>>(userManager, contextAccessor, userPrincipalFactory, null, null, null, null);
        signInManager.CheckPasswordSignInAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(SignInResult.Success);

        var mfaService = Substitute.For<IMfaService>();

        var jwtOptions = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Secret = "super_secret_jwt_key_at_least_32_characters_long_1234567890",
            Issuer = "CebizPay.Test",
            Audience = "CebizPay.ClientTest",
            ExpirationInMinutes = 15
        });

        var service = new IdentityService(
            userManager,
            signInManager,
            mfaService,
            jwtOptions,
            db,
            NullLogger<IdentityService>.Instance);

        return (service, db);
    }

    [Fact]
    public async Task ConcurrentRegistration_50SimultaneousRequests_MustPermitExactlyOneIdentity()
    {
        // Arrange
        await using var initDb = CreateContext();
        await initDb.Database.EnsureCreatedAsync();

        const int concurrencyDegree = 50;
        var phoneVariations = new[]
        {
            "08039991111",
            "+2348039991111",
            "2348039991111",
            "8039991111",
            "+234 803 999 1111",
            "+234-803-999-1111",
            "0803.999.1111",
            "+234 (0) 803 999 1111"
        };

        var tasks = new List<Task<(bool Succeeded, string UserId, IEnumerable<string> Errors)>>();
        var contexts = new List<ApplicationDbContext>();

        for (int i = 0; i < concurrencyDegree; i++)
        {
            var email = $"concurrent_user_{i}_{Guid.NewGuid():N}@example.com";
            var phone = phoneVariations[i % phoneVariations.Length];

            var (service, context) = CreateIdentityService();
            contexts.Add(context);

            tasks.Add(Task.Run(async () =>
            {
                return await service.RegisterUserAsync(email, "StrongPassword123!", phone);
            }));
        }

        // Act: Fire all 50 registration attempts simultaneously
        var results = await Task.WhenAll(tasks);

        // Assert: Exactly 1 must have succeeded
        var successfulResults = results.Where(r => r.Succeeded).ToList();
        var failedResults = results.Where(r => !r.Succeeded).ToList();

        Assert.Single(successfulResults);
        Assert.Equal(concurrencyDegree - 1, failedResults.Count);

        // Verify all failures returned safe, expected duplicate error message
        foreach (var failure in failedResults)
        {
            Assert.Contains(failure.Errors, e =>
                e.Contains("Phone number", StringComparison.OrdinalIgnoreCase));
        }

        // Authoritative Database check in PostgreSQL: exactly 1 row with canonical phone
        await using var verifyDb = CreateContext();
        var canonical = PhoneNormalizer.NormalizeE164("08039991111");
        var dbCount = await verifyDb.Users.CountAsync(u => u.PhoneNumber == canonical);
        Assert.Equal(1, dbCount);

        // Clean up contexts
        foreach (var ctx in contexts)
        {
            await ctx.DisposeAsync();
        }
    }

    [Fact]
    public async Task AccountLifecycle_LockedOrUnverifiedAccount_MustNotAllowPhoneReuse()
    {
        // Arrange
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();

        var (service1, ctx1) = CreateIdentityService();
        var (service2, ctx2) = CreateIdentityService();

        try
        {
            var phone = "08055556666";
            var canonical = PhoneNormalizer.NormalizeE164(phone);

            // 1. First user registers
            var (succeeded1, userId1, _) = await service1.RegisterUserAsync(
                "locked_owner@example.com", "Password123!", phone);
            Assert.True(succeeded1);

            // 2. Simulate account lockout / disablement in Identity
            var user1 = await ctx1.Users.FirstAsync(u => u.Id == userId1);
            user1.LockoutEnabled = true;
            user1.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            user1.EmailConfirmed = false;
            user1.PhoneNumberConfirmed = false;
            await ctx1.SaveChangesAsync();

            // 3. Another user tries to claim the same phone
            var (succeeded2, _, errors2) = await service2.RegisterUserAsync(
                "new_intruder@example.com", "Password123!", "+2348055556666");

            // Assert: Phone remains strictly bound to the original identity
            Assert.False(succeeded2);
            Assert.Contains(errors2, e => e.Contains("Phone number is already registered", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await ctx1.DisposeAsync();
            await ctx2.DisposeAsync();
        }
    }
}
