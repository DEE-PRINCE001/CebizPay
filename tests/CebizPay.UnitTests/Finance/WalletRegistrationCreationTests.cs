using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Admin.Wallets;
using CebizPay.Application.UseCases.Auth.VerifyOtp;
using CebizPay.Application.UseCases.Organizations.RegisterStep1;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Finance;

public sealed class WalletRegistrationCreationTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task VerifyOtp_SuccessfulRegistration_MustCreateIndividualWallet()
    {
        // Arrange
        await using var db = CreateDbContext();
        var otpService = Substitute.For<IOtpService>();
        otpService.VerifyOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var identityService = Substitute.For<IIdentityService>();
        var generatedUserId = Guid.NewGuid().ToString();
        identityService.RegisterUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((true, generatedUserId, Enumerable.Empty<string>()));
        identityService.LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((true, generatedUserId, "token", "refresh", false, (Guid?)null, Enumerable.Empty<string>()));

        var eventPublisher = Substitute.For<IEventPublisher>();
        var walletService = new WalletService(db);

        var handler = new VerifyOtpCommandHandler(
            otpService,
            identityService,
            db,
            eventPublisher,
            walletService);

        var command = new VerifyOtpCommand(
            Phone: "08012345678",
            Code: "123456",
            Email: "user@example.com",
            FirstName: "Test",
            LastName: "User",
            Password: "Password123!");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(generatedUserId, result.UserId);

        var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.IndividualId == generatedUserId && w.Currency == Currency.NGN);
        Assert.NotNull(wallet);
        Assert.Equal(0m, wallet.AvailableBalance);
        Assert.Equal(WalletStatus.Active, wallet.Status);

        var ledgerAccount = await db.LedgerAccounts.FirstOrDefaultAsync(l => l.WalletId == wallet.Id);
        Assert.NotNull(ledgerAccount);
        Assert.Equal(Currency.NGN, ledgerAccount.Currency);
    }

    [Fact]
    public async Task RegisterStep1_MustCreateOrganizationWallet()
    {
        // Arrange
        await using var db = CreateDbContext();
        var eventPublisher = Substitute.For<IEventPublisher>();
        var walletService = new WalletService(db);
        var ownerUserId = Guid.NewGuid().ToString();

        var handler = new RegisterStep1CommandHandler(db, eventPublisher, walletService);

        var command = new RegisterStep1Command(
            CompanyName: "Acme Corp",
            Email: "acme@example.com",
            Phone: "08099887766",
            OwnerUserId: ownerUserId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result.OrganizationId);

        var orgWallet = await db.Wallets.FirstOrDefaultAsync(w => w.OrganizationId == result.OrganizationId && w.Currency == Currency.NGN);
        Assert.NotNull(orgWallet);
        Assert.Equal(0m, orgWallet.AvailableBalance);
        Assert.Equal(WalletStatus.Active, orgWallet.Status);

        var ledgerAccount = await db.LedgerAccounts.FirstOrDefaultAsync(l => l.WalletId == orgWallet.Id);
        Assert.NotNull(ledgerAccount);
        Assert.Equal(Currency.NGN, ledgerAccount.Currency);
    }

    [Fact]
    public async Task BackfillMissingWallets_ShouldProvisionWalletsForMissingEntitiesOnly()
    {
        // Arrange
        await using var db = CreateDbContext();
        var walletService = new WalletService(db);

        // 1. User with existing wallet
        var existingUserId = Guid.NewGuid().ToString();
        db.IndividualProfiles.Add(new IndividualProfile(existingUserId, "Existing", "User"));
        await walletService.GetOrCreateIndividualWalletAsync(existingUserId, Currency.NGN);

        // 2. User missing wallet
        var missingUserId1 = Guid.NewGuid().ToString();
        var missingUserId2 = Guid.NewGuid().ToString();
        db.IndividualProfiles.Add(new IndividualProfile(missingUserId1, "Missing", "User1"));
        db.IndividualProfiles.Add(new IndividualProfile(missingUserId2, "Missing", "User2"));

        // 3. Organization with existing wallet
        var existingOrg = new Organization("Existing Org", "exist@org.com", "08011111111");
        db.Organizations.Add(existingOrg);
        await walletService.GetOrCreateOrganizationWalletAsync(existingOrg.Id, Currency.NGN);

        // 4. Organization missing wallet
        var missingOrg = new Organization("Missing Org", "missing@org.com", "08022222222");
        db.Organizations.Add(missingOrg);

        await db.SaveChangesAsync();

        var handler = new BackfillMissingWalletsCommandHandler(db, walletService);
        var command = new BackfillMissingWalletsCommand(Currency.NGN);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.IndividualWalletsCreated);
        Assert.Equal(1, result.OrganizationWalletsCreated);

        // Check that missing users now have wallets
        Assert.NotNull(await db.Wallets.FirstOrDefaultAsync(w => w.IndividualId == missingUserId1 && w.Currency == Currency.NGN));
        Assert.NotNull(await db.Wallets.FirstOrDefaultAsync(w => w.IndividualId == missingUserId2 && w.Currency == Currency.NGN));
        Assert.NotNull(await db.Wallets.FirstOrDefaultAsync(w => w.OrganizationId == missingOrg.Id && w.Currency == Currency.NGN));

        // Re-run backfill should be completely idempotent (0 created)
        var rerunResult = await handler.Handle(command, CancellationToken.None);
        Assert.Equal(0, rerunResult.IndividualWalletsCreated);
        Assert.Equal(0, rerunResult.OrganizationWalletsCreated);
    }
}
