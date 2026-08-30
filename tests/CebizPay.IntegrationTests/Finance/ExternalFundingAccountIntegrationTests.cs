using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.IntegrationTests.Finance;

/// <summary>
/// PostgreSQL Testcontainers integration tests for <see cref="ExternalFundingAccount"/> and <see cref="ExternalFundingAccountService"/>.
/// Validates database-level partial unique index concurrency guarantees and service operations.
/// </summary>
public sealed class ExternalFundingAccountIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;
    private readonly IOutboxService _outbox = Substitute.For<IOutboxService>();

    public ExternalFundingAccountIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ApplicationDbContext> CreateDbContextAsync()
    {
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Fact]
    public async Task DatabaseConstraint_MultiplePrimaryAccountsOnSameWallet_ShouldThrowDbUpdateException()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var wallet = Wallet.CreateIndividualWallet($"user_{Guid.NewGuid():N}", Currency.NGN);
        dbContext.Wallets.Add(wallet);
        await dbContext.SaveChangesAsync();

        var account1 = ExternalFundingAccount.Create(
            walletId: wallet.Id,
            provider: PaymentProvider.Monnify,
            accountNumber: $"{Random.Shared.Next(1000000000, 2000000000)}",
            accountName: "Primary Account 1",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            isPrimary: true);

        dbContext.ExternalFundingAccounts.Add(account1);
        await dbContext.SaveChangesAsync();

        // Act & Assert: Attempting to insert a second account with IsPrimary = true directly to DB
        var account2 = ExternalFundingAccount.Create(
            walletId: wallet.Id,
            provider: PaymentProvider.Monnify,
            accountNumber: $"{Random.Shared.Next(2000000001, int.MaxValue)}",
            accountName: "Primary Account 2",
            bankCode: "058",
            bankName: "GTBank",
            currency: Currency.NGN,
            isPrimary: true);

        dbContext.ExternalFundingAccounts.Add(account2);

        // PostgreSQL partial unique index IX_ExternalFundingAccounts_WalletId_IsPrimary must reject this at the engine level
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task SetPrimaryAccountAsync_ShouldAtomicallySwitchPrimaryAccount()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var service = new ExternalFundingAccountService(dbContext, _outbox, Enumerable.Empty<CebizPay.Application.Common.Interfaces.Payments.IVirtualAccountProvider>(), NullLogger<ExternalFundingAccountService>.Instance);

        var userId = $"user_{Guid.NewGuid():N}";
        var wallet = Wallet.CreateIndividualWallet(userId, Currency.NGN);
        dbContext.Wallets.Add(wallet);
        await dbContext.SaveChangesAsync();

        var account1 = await service.CreateAccountAsync(
            walletId: wallet.Id,
            provider: PaymentProvider.Monnify,
            accountNumber: $"{Random.Shared.Next(1000000000, 2000000000)}",
            accountName: "First Account",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            isPrimary: true);

        var account2 = await service.CreateAccountAsync(
            walletId: wallet.Id,
            provider: PaymentProvider.Monnify,
            accountNumber: $"{Random.Shared.Next(2000000001, int.MaxValue)}",
            accountName: "Second Account",
            bankCode: "058",
            bankName: "GTBank",
            currency: Currency.NGN,
            isPrimary: false);

        // Act: Switch primary to account 2
        var updated = await service.SetPrimaryAccountAsync(account2.Id, userId);

        // Assert
        Assert.True(updated.IsPrimary);

        var primaryInDb = await service.GetPrimaryAccountForWalletAsync(wallet.Id);
        Assert.NotNull(primaryInDb);
        Assert.Equal(account2.Id, primaryInDb.Id);

        var allAccounts = await service.GetAccountsForWalletAsync(wallet.Id);
        Assert.Equal(2, allAccounts.Count);
        Assert.Single(allAccounts, a => a.IsPrimary);
    }

    [Fact]
    public async Task SetPrimaryAccountAsync_CrossTenantAccess_ShouldThrowTransferNotAuthorizedException()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var service = new ExternalFundingAccountService(dbContext, _outbox, Enumerable.Empty<CebizPay.Application.Common.Interfaces.Payments.IVirtualAccountProvider>(), NullLogger<ExternalFundingAccountService>.Instance);

        var userA = $"user_a_{Guid.NewGuid():N}";
        var userB = $"user_b_{Guid.NewGuid():N}";

        var walletA = Wallet.CreateIndividualWallet(userA, Currency.NGN);
        dbContext.Wallets.Add(walletA);
        await dbContext.SaveChangesAsync();

        var accountA = await service.CreateAccountAsync(
            walletId: walletA.Id,
            provider: PaymentProvider.Monnify,
            accountNumber: $"{Random.Shared.Next(1000000000, 2000000000)}",
            accountName: "User A Account",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            isPrimary: false);

        // Act & Assert: User B attempts to manipulate User A's account
        await Assert.ThrowsAsync<TransferNotAuthorizedException>(() =>
            service.SetPrimaryAccountAsync(accountA.Id, userB));
    }
}
