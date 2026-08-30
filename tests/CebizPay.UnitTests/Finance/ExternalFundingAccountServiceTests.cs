using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Finance;

/// <summary>
/// Unit tests for <see cref="ExternalFundingAccountService"/>.
/// Validates primary switching, single primary constraint, outbox event generation, and security boundaries.
/// </summary>
public sealed class ExternalFundingAccountServiceTests
{
    private readonly IOutboxService _outbox = Substitute.For<IOutboxService>();

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private ExternalFundingAccountService CreateService(ApplicationDbContext dbContext, IEnumerable<CebizPay.Application.Common.Interfaces.Payments.IVirtualAccountProvider>? providers = null)
    {
        return new ExternalFundingAccountService(
            dbContext,
            _outbox,
            providers ?? Enumerable.Empty<CebizPay.Application.Common.Interfaces.Payments.IVirtualAccountProvider>(),
            NullLogger<ExternalFundingAccountService>.Instance);
    }

    [Fact]
    public async Task CreateAccountAsync_ValidParameters_ShouldPersistAndPublishOutboxEvent()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var wallet = Wallet.CreateIndividualWallet("user-101", Currency.NGN);
        dbContext.Wallets.Add(wallet);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.CreateAccountAsync(
            walletId: wallet.Id,
            provider: PaymentProvider.Monnify,
            accountNumber: "9876543210",
            accountName: "Jane Doe",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            providerCustomerReference: "MNFY_CUST_101",
            providerAccountReference: "MNFY_ACC_101",
            isPrimary: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(wallet.Id, result.WalletId);
        Assert.Equal("9876543210", result.AccountNumber);
        Assert.True(result.IsPrimary);

        var savedAccount = await dbContext.ExternalFundingAccounts.FirstOrDefaultAsync(a => a.Id == result.Id);
        Assert.NotNull(savedAccount);
        Assert.True(savedAccount.IsPrimary);

        _outbox.Received(1).Write(Arg.Any<object>());
    }

    [Fact]
    public async Task SetPrimaryAccountAsync_ShouldAtomicallyClearPreviousPrimary()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var wallet = Wallet.CreateIndividualWallet("user-101", Currency.NGN);
        dbContext.Wallets.Add(wallet);

        var account1 = ExternalFundingAccount.Create(
            walletId: wallet.Id,
            provider: PaymentProvider.Monnify,
            accountNumber: "1111111111",
            accountName: "User Acct 1",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            isPrimary: true);

        var account2 = ExternalFundingAccount.Create(
            walletId: wallet.Id,
            provider: PaymentProvider.Monnify,
            accountNumber: "2222222222",
            accountName: "User Acct 2",
            bankCode: "058",
            bankName: "GTBank",
            currency: Currency.NGN,
            isPrimary: false);

        dbContext.ExternalFundingAccounts.AddRange(account1, account2);
        await dbContext.SaveChangesAsync();

        // Act: Set account2 as primary
        var updated = await service.SetPrimaryAccountAsync(account2.Id, "user-101");

        // Assert
        Assert.True(updated.IsPrimary);

        var refreshedAccount1 = await dbContext.ExternalFundingAccounts.FindAsync(account1.Id);
        var refreshedAccount2 = await dbContext.ExternalFundingAccounts.FindAsync(account2.Id);

        Assert.NotNull(refreshedAccount1);
        Assert.NotNull(refreshedAccount2);
        Assert.False(refreshedAccount1.IsPrimary);
        Assert.True(refreshedAccount2.IsPrimary);
    }

    [Fact]
    public async Task SetPrimaryAccountAsync_UnauthorizedUser_ShouldThrowTransferNotAuthorizedException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var wallet = Wallet.CreateIndividualWallet("user-101", Currency.NGN);
        dbContext.Wallets.Add(wallet);

        var account = ExternalFundingAccount.Create(
            walletId: wallet.Id,
            provider: PaymentProvider.Monnify,
            accountNumber: "1111111111",
            accountName: "User Acct 1",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            isPrimary: false);

        dbContext.ExternalFundingAccounts.Add(account);
        await dbContext.SaveChangesAsync();

        // Act & Assert: Attempt to set primary by different user
        await Assert.ThrowsAsync<TransferNotAuthorizedException>(() =>
            service.SetPrimaryAccountAsync(account.Id, "attacker-999"));
    }

    [Fact]
    public async Task UpdateStatusAsync_Suspended_ShouldRevokePrimaryStatus()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var wallet = Wallet.CreateIndividualWallet("user-101", Currency.NGN);
        dbContext.Wallets.Add(wallet);

        var account = ExternalFundingAccount.Create(
            walletId: wallet.Id,
            provider: PaymentProvider.Monnify,
            accountNumber: "1111111111",
            accountName: "User Acct 1",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            isPrimary: true);

        dbContext.ExternalFundingAccounts.Add(account);
        await dbContext.SaveChangesAsync();

        // Act: Suspend account
        var updated = await service.UpdateStatusAsync(
            account.Id,
            ExternalFundingAccountStatus.Suspended,
            "user-101");

        // Assert
        Assert.Equal(ExternalFundingAccountStatus.Suspended, updated.Status);
        Assert.False(updated.IsPrimary);

        var refreshed = await dbContext.ExternalFundingAccounts.FindAsync(account.Id);
        Assert.NotNull(refreshed);
        Assert.False(refreshed.IsPrimary);
    }

    [Fact]
    public async Task ProvisionMonnifyFundingAccountAsync_ShouldCallProviderAndPersistAccount()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var mockProvider = Substitute.For<CebizPay.Application.Common.Interfaces.Payments.IVirtualAccountProvider>();
        mockProvider.Provider.Returns(PaymentProvider.Monnify);
        mockProvider.CreateVirtualAccountAsync(Arg.Any<CebizPay.Application.Common.Interfaces.Payments.VirtualAccountCreationRequest>(), Arg.Any<CancellationToken>())
            .Returns(CebizPay.Application.Common.Interfaces.Payments.VirtualAccountCreationResult.Success(
                accountNumber: "8899001122",
                accountName: "John Doe",
                bankCode: "035",
                bankName: "Wema Bank",
                providerReference: "MNFY_REF_AUTO"));

        var service = CreateService(dbContext, new[] { mockProvider });

        var wallet = Wallet.CreateIndividualWallet("user-prov-1", Currency.NGN);
        dbContext.Wallets.Add(wallet);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.ProvisionMonnifyFundingAccountAsync(wallet.Id, "user-prov-1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("8899001122", result.AccountNumber);
        Assert.Equal("Wema Bank", result.BankName);
        Assert.Equal(PaymentProvider.Monnify, result.Provider);
        Assert.True(result.IsPrimary); // First account is marked primary

        var dbAccount = await dbContext.ExternalFundingAccounts.FirstOrDefaultAsync(a => a.WalletId == wallet.Id);
        Assert.NotNull(dbAccount);
        Assert.Equal("8899001122", dbAccount.AccountNumber);
    }

    [Fact]
    public async Task ProvisionMonnifyFundingAccountAsync_WhenActiveAccountAlreadyExists_ShouldReturnExistingIdempotently()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var mockProvider = Substitute.For<CebizPay.Application.Common.Interfaces.Payments.IVirtualAccountProvider>();
        mockProvider.Provider.Returns(PaymentProvider.Monnify);

        var service = CreateService(dbContext, new[] { mockProvider });

        var wallet = Wallet.CreateIndividualWallet("user-prov-2", Currency.NGN);
        dbContext.Wallets.Add(wallet);

        var existing = ExternalFundingAccount.Create(
            walletId: wallet.Id,
            provider: PaymentProvider.Monnify,
            accountNumber: "1234567890",
            accountName: "Existing Name",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN,
            isPrimary: true);
        dbContext.ExternalFundingAccounts.Add(existing);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.ProvisionMonnifyFundingAccountAsync(wallet.Id, "user-prov-2");

        // Assert
        Assert.Equal(existing.Id, result.Id);
        Assert.Equal("1234567890", result.AccountNumber);
        await mockProvider.DidNotReceive().CreateVirtualAccountAsync(Arg.Any<CebizPay.Application.Common.Interfaces.Payments.VirtualAccountCreationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAccountByIdAsync_AuthorizedUser_ShouldReturnDto()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        var wallet = Wallet.CreateIndividualWallet("user-get-1", Currency.NGN);
        dbContext.Wallets.Add(wallet);

        var account = ExternalFundingAccount.Create(
            walletId: wallet.Id,
            provider: PaymentProvider.Monnify,
            accountNumber: "5555555555",
            accountName: "Get Acct",
            bankCode: "035",
            bankName: "Wema Bank",
            currency: Currency.NGN);
        dbContext.ExternalFundingAccounts.Add(account);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.GetAccountByIdAsync(account.Id, "user-get-1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(account.Id, result.Id);
        Assert.Equal("5555555555", result.AccountNumber);
    }
}
