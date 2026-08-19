using CebizPay.Infrastructure.Identity;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CebizPay.IntegrationTests.Security;

public sealed class TransactionPinLockoutIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public TransactionPinLockoutIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private ApplicationDbContext CreateDbContext()
    {
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task VerifyPin_ThreeConsecutiveFailuresInPostgres_Activates15MinuteDebitLock_AndBlocksSubsequentAttempts()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var userId = $"pin_lockout_user_{Guid.NewGuid():N}";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@example.com",
            Email = $"{userId}@example.com",
            TransactionPinHash = BCrypt.Net.BCrypt.HashPassword("1234"),
            FailedPinAttempts = 0,
            PinLockoutEndUtc = null
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        userManager.FindByIdAsync(userId).Returns(user);

        var pinService = new TransactionPinService(userManager, dbContext);

        // Act 1: 1st wrong attempt
        var result1 = await pinService.VerifyPinAsync(userId, "9999");
        Assert.False(result1.Succeeded);
        Assert.False(result1.IsLocked);
        Assert.Contains("Attempts remaining: 2", result1.Error);

        var userInDb1 = await dbContext.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        Assert.Equal(1, userInDb1.FailedPinAttempts);
        Assert.Null(userInDb1.PinLockoutEndUtc);

        // Act 2: 2nd wrong attempt
        var result2 = await pinService.VerifyPinAsync(userId, "8888");
        Assert.False(result2.Succeeded);
        Assert.False(result2.IsLocked);
        Assert.Contains("Attempts remaining: 1", result2.Error);

        var userInDb2 = await dbContext.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        Assert.Equal(2, userInDb2.FailedPinAttempts);
        Assert.Null(userInDb2.PinLockoutEndUtc);

        // Act 3: 3rd wrong attempt -> triggers 15-minute lock
        var result3 = await pinService.VerifyPinAsync(userId, "7777");
        Assert.False(result3.Succeeded);
        Assert.True(result3.IsLocked);
        Assert.Contains("lock activated for 15 minutes", result3.Error);

        var userInDb3 = await dbContext.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        Assert.Equal(3, userInDb3.FailedPinAttempts);
        Assert.NotNull(userInDb3.PinLockoutEndUtc);
        Assert.True(userInDb3.PinLockoutEndUtc > DateTime.UtcNow.AddMinutes(14));

        // Act 4: 4th attempt (even with CORRECT PIN) -> blocked by debit lock
        var result4 = await pinService.VerifyPinAsync(userId, "1234");
        Assert.False(result4.Succeeded);
        Assert.True(result4.IsLocked);
        Assert.Contains("debit lock active", result4.Error);
    }
}
