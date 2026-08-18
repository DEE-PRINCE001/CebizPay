using CebizPay.Infrastructure.Identity;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Security;

public sealed class TransactionPinServiceTests : IDisposable
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly TransactionPinService _service;

    public TransactionPinServiceTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ApplicationDbContext(options);

        _service = new TransactionPinService(_userManager, _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task SetPinAsync_Valid4DigitPin_ShouldStoreBcryptHash_NotPlaintextOrSha256()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-123" };
        _userManager.FindByIdAsync("user-123").Returns(Task.FromResult<ApplicationUser?>(user));
        _userManager.UpdateAsync(user).Returns(Task.FromResult(IdentityResult.Success));
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.SetPinAsync("user-123", "1234");

        // Assert
        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
        Assert.NotNull(user.TransactionPinHash);
        Assert.NotEqual("1234", user.TransactionPinHash); // Not plaintext

        // Verify hash structure is a valid BCrypt hash ($2a$, $2b$, or $2y$)
        Assert.True(
            user.TransactionPinHash.StartsWith("$2a$", StringComparison.Ordinal) ||
            user.TransactionPinHash.StartsWith("$2b$", StringComparison.Ordinal) ||
            user.TransactionPinHash.StartsWith("$2y$", StringComparison.Ordinal),
            "Stored PIN hash must be a valid bcrypt hash signature.");

        Assert.True(BCrypt.Net.BCrypt.Verify("1234", user.TransactionPinHash));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("12a4")]
    public async Task SetPinAsync_InvalidPinFormat_ShouldFail(string invalidPin)
    {
        // Act
        var result = await _service.SetPinAsync("user-123", invalidPin);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("PIN must be exactly 4 numeric digits.", result.Error);
    }

    [Fact]
    public async Task VerifyPinAsync_CorrectPin_ShouldSucceed()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-123" };
        _userManager.FindByIdAsync("user-123").Returns(Task.FromResult<ApplicationUser?>(user));
        _userManager.UpdateAsync(user).Returns(Task.FromResult(IdentityResult.Success));
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        await _service.SetPinAsync("user-123", "4321");
        await _dbContext.SaveChangesAsync();

        // Act
        var verifyResult = await _service.VerifyPinAsync("user-123", "4321");

        // Assert
        Assert.True(verifyResult.Succeeded);
        Assert.False(verifyResult.IsLocked);
        Assert.Null(verifyResult.Error);
    }

    [Fact]
    public async Task VerifyPinAsync_IncorrectPin_ShouldFail()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-123" };
        _userManager.FindByIdAsync("user-123").Returns(Task.FromResult<ApplicationUser?>(user));
        _userManager.UpdateAsync(user).Returns(Task.FromResult(IdentityResult.Success));
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        await _service.SetPinAsync("user-123", "4321");
        await _dbContext.SaveChangesAsync();

        // Act
        var verifyResult = await _service.VerifyPinAsync("user-123", "9999");

        // Assert
        Assert.False(verifyResult.Succeeded);
        Assert.False(verifyResult.IsLocked);
        Assert.NotNull(verifyResult.Error);
    }

    [Fact]
    public async Task VerifyPinAsync_ThreeFailedAttempts_ShouldActivate15MinuteDebitLock()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-123" };
        _userManager.FindByIdAsync("user-123").Returns(Task.FromResult<ApplicationUser?>(user));
        _userManager.UpdateAsync(user).Returns(Task.FromResult(IdentityResult.Success));
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        await _service.SetPinAsync("user-123", "1234");
        await _dbContext.SaveChangesAsync();

        // Act - Attempt 1 (Wrong PIN)
        var attempt1 = await _service.VerifyPinAsync("user-123", "0000");
        await _dbContext.SaveChangesAsync();
        Assert.False(attempt1.Succeeded);
        Assert.False(attempt1.IsLocked);

        // Act - Attempt 2 (Wrong PIN)
        var attempt2 = await _service.VerifyPinAsync("user-123", "0000");
        await _dbContext.SaveChangesAsync();
        Assert.False(attempt2.Succeeded);
        Assert.False(attempt2.IsLocked);

        // Act - Attempt 3 (Wrong PIN) -> triggers lockout
        var attempt3 = await _service.VerifyPinAsync("user-123", "0000");
        await _dbContext.SaveChangesAsync();

        // Assert
        Assert.False(attempt3.Succeeded);
        Assert.True(attempt3.IsLocked);
        Assert.NotNull(user.PinLockoutEndUtc);
        Assert.True(user.PinLockoutEndUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task VerifyPinAsync_WhenActiveLockout_ShouldRemainLockedWithoutResettingUnrelatedLockoutState()
    {
        // Arrange
        var lockoutEnd = DateTime.UtcNow.AddMinutes(10);
        var user = new ApplicationUser
        {
            Id = "user-123",
            TransactionPinHash = BCrypt.Net.BCrypt.HashPassword("1234"),
            PinLockoutEndUtc = lockoutEnd,
            FailedPinAttempts = 3
        };
        _userManager.FindByIdAsync("user-123").Returns(Task.FromResult<ApplicationUser?>(user));
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act - Attempt verification while locked out
        var result = await _service.VerifyPinAsync("user-123", "1234");

        // Assert - Lockout remains active and end timestamp is unaltered
        Assert.False(result.Succeeded);
        Assert.True(result.IsLocked);
        Assert.Equal(lockoutEnd, user.PinLockoutEndUtc);
    }
}
