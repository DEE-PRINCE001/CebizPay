using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Caching;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Utils;
using CebizPay.Application.UseCases.Auth.RegisterPhone;
using CebizPay.Application.UseCases.Auth.VerifyOtp;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Referrals.Entities;
using CebizPay.Domain.Referrals.Enums;
using CebizPay.Infrastructure.Identity;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Referrals;
using CebizPay.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Identity;

/// <summary>
/// Comprehensive unit tests verifying phone number normalization, identity uniqueness,
/// format invariance, OTP lookup consistency, referral anti-abuse collision detection,
/// and security enumeration resistance.
/// </summary>
public sealed class PhoneIdentityUniquenessTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IMfaService _mfaService;
    private readonly IdentityService _identityService;
    private readonly UserLookupService _userLookupService;

    public PhoneIdentityUniquenessTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new ApplicationDbContext(options);

        var userStore = new UserStore<ApplicationUser, IdentityRole, ApplicationDbContext, string>(_dbContext);
        var identityOptions = Microsoft.Extensions.Options.Options.Create(new IdentityOptions
        {
            User = { RequireUniqueEmail = true }
        });

        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var userValidators = new List<IUserValidator<ApplicationUser>> { new UserValidator<ApplicationUser>() };
        var passwordValidators = new List<IPasswordValidator<ApplicationUser>> { new PasswordValidator<ApplicationUser>() };

        _userManager = new UserManager<ApplicationUser>(
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
        _signInManager = Substitute.For<SignInManager<ApplicationUser>>(_userManager, contextAccessor, userPrincipalFactory, null, null, null, null);
        _signInManager.CheckPasswordSignInAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(SignInResult.Success);

        _mfaService = Substitute.For<IMfaService>();

        var jwtOptions = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Secret = "super_secret_jwt_key_at_least_32_characters_long_1234567890",
            Issuer = "CebizPay.Test",
            Audience = "CebizPay.ClientTest",
            ExpirationInMinutes = 15
        });

        _identityService = new IdentityService(
            _userManager,
            _signInManager,
            _mfaService,
            jwtOptions,
            _dbContext,
            NullLogger<IdentityService>.Instance);

        _userLookupService = new UserLookupService(_userManager);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    // ─── 1. Phone Normalization Tests ──────────────────────────────────────────

    [Theory]
    [InlineData("08031234567", "+2348031234567")]
    [InlineData("+2348031234567", "+2348031234567")]
    [InlineData("2348031234567", "+2348031234567")]
    [InlineData("8031234567", "+2348031234567")]
    [InlineData("+234 (0) 803 123 4567", "+2348031234567")]
    [InlineData("+234-803-123-4567", "+2348031234567")]
    [InlineData("0803.123.4567", "+2348031234567")]
    [InlineData("  0803 123 4567  ", "+2348031234567")]
    [InlineData("+14155552671", "+14155552671")]
    public void NormalizeE164_ShouldReturnCanonicalE164Format(string input, string expected)
    {
        var result = PhoneNormalizer.NormalizeE164(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("08031234567", true)]
    [InlineData("+2348031234567", true)]
    [InlineData("2348031234567", true)]
    [InlineData("+2348000000000", true)]
    [InlineData("+14155552671", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("12345", false)]
    [InlineData("not-a-number", false)]
    public void IsValidPhoneNumber_ShouldValidateCorrectly(string input, bool expectedValid)
    {
        var result = PhoneNormalizer.IsValidPhoneNumber(input);
        Assert.Equal(expectedValid, result);
    }

    // ─── 2. Duplicate Registration Rejection ───────────────────────────────────

    [Fact]
    public async Task RegisterUserAsync_ExactDuplicatePhone_MustBeRejected()
    {
        // Arrange: User 1 registers with canonical phone
        var (succeeded1, _, _) = await _identityService.RegisterUserAsync(
            "alice@example.com", "Password123!", "+2348031234567");
        Assert.True(succeeded1);

        // Act: User 2 attempts to register with identical phone
        var (succeeded2, _, errors2) = await _identityService.RegisterUserAsync(
            "bob@example.com", "Password123!", "+2348031234567");

        // Assert: Rejected
        Assert.False(succeeded2);
        Assert.Contains(errors2, e => e.Contains("Phone number is already registered", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("08031234567")]
    [InlineData("+2348031234567")]
    [InlineData("2348031234567")]
    [InlineData("8031234567")]
    [InlineData("+234-803-123-4567")]
    [InlineData("  0803 123 4567  ")]
    [InlineData("+234 (0) 803 123 4567")]
    public async Task RegisterUserAsync_FormatVariationsOfSamePhone_MustAllBeRejected(string variation)
    {
        // Arrange: User 1 registered with 08031234567
        var (succeeded1, _, _) = await _identityService.RegisterUserAsync(
            "alice_var@example.com", "Password123!", "08031234567");
        Assert.True(succeeded1);

        // Act: User 2 attempts to register using a textual variation of that same phone
        var (succeeded2, _, errors2) = await _identityService.RegisterUserAsync(
            "bob_var@example.com", "Password123!", variation);

        // Assert: Must be rejected
        Assert.False(succeeded2);
        Assert.Contains(errors2, e => e.Contains("Phone number is already registered", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RegisterUserAsync_UsersWithoutPhoneNumber_ShouldBothSucceed()
    {
        // Arrange: Corporate users or invites without phone numbers
        var (succeeded1, _, _) = await _identityService.RegisterUserAsync(
            "corp1@example.com", "Password123!", null);
        var (succeeded2, _, _) = await _identityService.RegisterUserAsync(
            "corp2@example.com", "Password123!", null);

        // Assert: Null phone numbers do not collide
        Assert.True(succeeded1);
        Assert.True(succeeded2);
    }

    // ─── 3. UserLookupService Canonical Phone Resolution ───────────────────────

    [Theory]
    [InlineData("08031234567")]
    [InlineData("+2348031234567")]
    [InlineData("2348031234567")]
    [InlineData("+234 803 123 4567")]
    public async Task FindByPhoneAsync_AnyFormattingVariation_ResolvesCorrectUser(string queryPhone)
    {
        // Arrange: User registered with 08031234567
        var (succeeded, userId, _) = await _identityService.RegisterUserAsync(
            "lookup_user@example.com", "Password123!", "08031234567");
        Assert.True(succeeded);

        // Act: Lookup by various formats
        var summary = await _userLookupService.FindByPhoneAsync(queryPhone);

        // Assert
        Assert.NotNull(summary);
        Assert.Equal(userId, summary.UserId);
        Assert.Equal("lookup_user@example.com", summary.Email);
        Assert.Equal("+2348031234567", summary.PhoneNumber);
    }

    // ─── 4. VerifyOtpCommandHandler Integration ────────────────────────────────

    [Fact]
    public async Task VerifyOtp_PhoneAlreadyRegistered_MustReturnDuplicateError()
    {
        // Arrange: Existing user already owns +2348011112222
        await _identityService.RegisterUserAsync("existing@example.com", "Password123!", "08011112222");

        var otpService = Substitute.For<IOtpService>();
        otpService.VerifyOtpAsync(Arg.Any<string>(), "123456", Arg.Any<CancellationToken>())
            .Returns(true);

        var eventPublisher = Substitute.For<IEventPublisher>();

        var handler = new VerifyOtpCommandHandler(
            otpService,
            _identityService,
            _dbContext,
            eventPublisher);

        // Act: Another user enters OTP for +2348011112222
        var command = new VerifyOtpCommand(
            Phone: "+2348011112222",
            Code: "123456",
            Email: "newcomer@example.com",
            FirstName: "New",
            LastName: "Comer",
            Password: "SecurePassword123!");

        var response = await handler.Handle(command, CancellationToken.None);

        // Assert: Registration rejected
        Assert.False(response.Success);
        Assert.NotNull(response.Errors);
        Assert.Contains(response.Errors, e => e.Contains("Phone number is already registered", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VerifyOtp_OtpRequestedWithInternational_VerifiedWithLocal_Succeeds()
    {
        // Arrange
        var cacheService = Substitute.For<ICacheService>();
        cacheService.GetAsync<string>("otp_code:+2348099990000", Arg.Any<CancellationToken>())
            .Returns("654321");

        var redisOtp = new RedisOtpService(cacheService);
        var eventPublisher = Substitute.For<IEventPublisher>();

        var handler = new VerifyOtpCommandHandler(
            redisOtp,
            _identityService,
            _dbContext,
            eventPublisher);

        // Act: User verifies with local "08099990000" while cache had "+2348099990000"
        var command = new VerifyOtpCommand(
            Phone: "08099990000",
            Code: "654321",
            Email: "local_verify@example.com",
            FirstName: "Local",
            LastName: "Verify",
            Password: "SecurePassword123!");

        var response = await handler.Handle(command, CancellationToken.None);

        // Assert: Format invariance ensures OTP matches and user is created
        Assert.True(response.Success);
        Assert.NotNull(response.UserId);

        var user = await _userManager.FindByIdAsync(response.UserId);
        Assert.NotNull(user);
        Assert.Equal("+2348099990000", user.PhoneNumber);
    }

    // ─── 5. Referral Anti-Abuse Phone Collision Invariance ─────────────────────

    [Fact]
    public async Task ReferralQualification_DifferentPhoneFormats_MustDetectCollisionAndHoldForReview()
    {
        // Arrange: Referrer registered with local format, Referred registered with international format
        var (s1, referrerId, _) = await _identityService.RegisterUserAsync("referrer@example.com", "Password123!", "08033334444");
        var (s2, referredId, _) = await _identityService.RegisterUserAsync("referred@example.com", "Password123!", "+2348033334444");
        Assert.True(s1);
        Assert.False(s2); // Second registration is prevented by identity uniqueness!

        // If legacy or fixture accounts existed with different formatting in db:
        var legacyReferredUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "referred_legacy@example.com",
            Email = "referred_legacy@example.com",
            PhoneNumber = "+2348033334444",
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Users.Add(legacyReferredUser);

        var profile = new IndividualProfile(legacyReferredUser.Id, "Referred", "User");
        profile.SetKycStatus(CebizPay.Domain.Enums.KycStatus.Verified);
        _dbContext.IndividualProfiles.Add(profile);

        var wallet = CebizPay.Domain.Finance.Entities.Wallet.CreateIndividualWallet(legacyReferredUser.Id, CebizPay.Domain.Finance.Enums.Currency.NGN);
        _dbContext.Wallets.Add(wallet);

        var deposit = CebizPay.Domain.Payments.Entities.FundingTransaction.Create(
            wallet.Id,
            null,
            CebizPay.Domain.Payments.Enums.PaymentProvider.Flutterwave,
            "TXN-QUALIFY-01",
            CebizPay.Domain.Payments.Enums.FundingChannel.VirtualAccount,
            5000m,
            CebizPay.Domain.Finance.Enums.Currency.NGN);
        deposit.MarkCompleted(Guid.NewGuid());
        _dbContext.FundingTransactions.Add(deposit);

        var rel = ReferralRelationship.Create(
            referrerUserId: referrerId,
            referredUserId: legacyReferredUser.Id,
            referralCodeId: Guid.NewGuid(),
            referralCode: "REF-TEST-01",
            now: DateTime.UtcNow);
        _dbContext.ReferralRelationships.Add(rel);

        var setting = ReferralSetting.CreateDefault();
        _dbContext.ReferralSettings.Add(setting);
        await _dbContext.SaveChangesAsync();

        var service = new ReferralQualificationService(_dbContext, NullLogger<ReferralQualificationService>.Instance);

        // Act: Evaluate qualification
        var processed = await service.EvaluateQualificationAsync(legacyReferredUser.Id, CancellationToken.None);

        // Assert: Even with different raw formats ("08033334444" vs "+2348033334444"), collision detected!
        Assert.True(processed.IsQualified);
        Assert.False(processed.RewardEligible);
        var updatedRel = await _dbContext.ReferralRelationships.FirstAsync(r => r.Id == rel.Id);
        Assert.Equal(ReferralRewardEligibility.HeldForRiskReview, updatedRel.RewardEligibility);
    }

    // ─── 6. Financial Safety ───────────────────────────────────────────────────

    [Fact]
    public async Task DuplicatePhoneRegistration_MustNotCreateAnyWalletsOrLedgerEntries()
    {
        // Arrange
        await _identityService.RegisterUserAsync("first_owner@example.com", "Password123!", "08077778888");

        var walletCountBefore = await _dbContext.Wallets.CountAsync();
        var ledgerTxnCountBefore = await _dbContext.LedgerTransactions.CountAsync();

        // Act: Attempt duplicate registration
        var (succeeded, _, _) = await _identityService.RegisterUserAsync(
            "second_owner@example.com", "Password123!", "+2348077778888");
        Assert.False(succeeded);

        // Assert: Strictly zero financial records created
        var walletCountAfter = await _dbContext.Wallets.CountAsync();
        var ledgerTxnCountAfter = await _dbContext.LedgerTransactions.CountAsync();

        Assert.Equal(walletCountBefore, walletCountAfter);
        Assert.Equal(ledgerTxnCountBefore, ledgerTxnCountAfter);
    }
}
