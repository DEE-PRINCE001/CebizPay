using System.Text;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Admin.Organizations;
using CebizPay.Application.UseCases.Admin.Wallets;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Loans.Entities;
using CebizPay.Domain.Loans.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Payroll.Entities;
using CebizPay.Domain.Payroll.Enums;
using CebizPay.Domain.Savings.Entities;
using CebizPay.Domain.Savings.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Admin;

public sealed class AdminWalletManagementUseCasesTests
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
    public async Task GetOrganizationWalletsDirectory_ShouldReturnAggregatesAndBalances()
    {
        await using var db = CreateDbContext();

        var org = new Organization("Cebis Tech", "cebis@gmail.com", "+2348011112222");
        db.Organizations.Add(org);

        var wallet = Wallet.CreateOrganizationWallet(org.Id, Currency.NGN);
        wallet.Credit(50000m);
        db.Wallets.Add(wallet);

        var batch = PayrollBatch.Create(
            org.Id,
            Currency.NGN,
            PayrollSelectionMode.All,
            DateTime.UtcNow.AddMonths(-1),
            DateTime.UtcNow,
            "initiator-1");
        db.PayrollBatches.Add(batch);

        var item = PayrollItem.Create(
            batch.Id,
            org.Id,
            "emp-1",
            "Alice",
            "alice@example.com",
            Currency.NGN,
            30000m,
            5000m);
        item.Claim("worker-1");
        item.MarkCompleted(Guid.NewGuid(), Guid.NewGuid());
        db.PayrollItems.Add(item);

        var app = LoanApplication.Create(
            org.Id,
            Guid.NewGuid(),
            "emp-1",
            "Alice",
            10000m,
            0.10m,
            6,
            1700m,
            600m,
            10600m,
            50000m,
            0m,
            1700m,
            1700m,
            0.034m,
            true,
            "Auto approved",
            RepaymentFrequency.Monthly,
            true);
        app.Approve("admin-1");
        var loan = LoanContract.CreateFromApplication(app);
        db.LoanContracts.Add(loan);

        await db.SaveChangesAsync();

        var handler = new GetAdminOrganizationWalletsDirectoryQueryHandler(db);
        var result = await handler.Handle(new GetAdminOrganizationWalletsDirectoryQuery(1, 10), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        var itemDto = result.Items[0];
        Assert.Equal(org.Id, itemDto.Id);
        Assert.Equal("Cebis Tech", itemDto.Name);
        Assert.Equal(50000m, itemDto.CurrentBalance);
        Assert.Equal(25000m, itemDto.TotalSalaryPaid);
        Assert.Equal(10000m, itemDto.TotalLoanPaid);
        Assert.Equal("NGN", itemDto.Currency);
    }

    [Fact]
    public async Task ExportOrganizationWallets_ShouldProduceValidCsv()
    {
        await using var db = CreateDbContext();

        var org = new Organization("Apex Finance", "apex@gmail.com", "+2348022223333");
        db.Organizations.Add(org);

        var wallet = Wallet.CreateOrganizationWallet(org.Id, Currency.NGN);
        wallet.Credit(120000m);
        db.Wallets.Add(wallet);

        await db.SaveChangesAsync();

        var handler = new ExportAdminOrganizationWalletsQueryHandler(db);
        var result = await handler.Handle(new ExportAdminOrganizationWalletsQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("text/csv; charset=utf-8", result.ContentType);
        Assert.Equal("organization_wallets_export.csv", result.FileName);

        var csvContent = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("Apex Finance", csvContent);
        Assert.Contains("120000.00", csvContent);
    }

    [Fact]
    public async Task GetOrganizationWallet_ShouldReturnDetailsAndVirtualAccount()
    {
        await using var db = CreateDbContext();

        var org = new Organization("Global Corp", "corp@gmail.com", "+2348033334444");
        db.Organizations.Add(org);

        var wallet = Wallet.CreateOrganizationWallet(org.Id, Currency.NGN);
        wallet.Credit(75000m);
        db.Wallets.Add(wallet);

        var va = VirtualAccount.CreateOrganization(
            org.Id,
            PaymentProvider.Flutterwave,
            "1234567890",
            "Global Corp Ltd",
            "035",
            "Wema Bank",
            Currency.NGN);
        db.VirtualAccounts.Add(va);

        await db.SaveChangesAsync();

        var handler = new GetAdminOrganizationWalletQueryHandler(db);
        var result = await handler.Handle(new GetAdminOrganizationWalletQuery(org.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(org.Id, result.OrganizationId);
        Assert.Equal(75000m, result.CurrentBalance);
        Assert.Equal("1234567890", result.VirtualAccountNumber);
        Assert.Equal("Wema Bank", result.BankName);
        Assert.StartsWith("WAL-ORG-", result.WalletId);
    }

    [Fact]
    public async Task GetOrganizationSalaries_ShouldReturnPaginatedLineItems()
    {
        await using var db = CreateDbContext();

        var org = new Organization("Salaries Inc", "sal@gmail.com", "+2348044445555");
        db.Organizations.Add(org);

        var batch = PayrollBatch.Create(
            org.Id,
            Currency.NGN,
            PayrollSelectionMode.All,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            "initiator-1");
        db.PayrollBatches.Add(batch);

        var item1 = PayrollItem.Create(
            batch.Id,
            org.Id,
            "emp-01",
            "Bob Smith",
            "bob@example.com",
            Currency.NGN,
            50000m,
            5000m);
        item1.Claim("worker-1");
        item1.MarkCompleted(Guid.NewGuid(), Guid.NewGuid());

        var item2 = PayrollItem.Create(
            batch.Id,
            org.Id,
            "emp-02",
            "Charlie Brown",
            "charlie@example.com",
            Currency.NGN,
            60000m,
            6000m);
        item2.Claim("worker-1");
        item2.MarkFailed("RETRY_EXHAUSTED", "Insufficient balance");

        db.PayrollItems.AddRange(item1, item2);
        await db.SaveChangesAsync();

        var handler = new GetAdminOrganizationSalariesQueryHandler(db);
        var result = await handler.Handle(new GetAdminOrganizationSalariesQuery(org.Id, 1, 10), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);

        var completedItem = result.Items.First(i => i.Amount == 45000m);
        Assert.Equal("Successfull", completedItem.Status);
        Assert.Equal("January", completedItem.Month);
        Assert.StartsWith("sal-", completedItem.Id);

        var failedItem = result.Items.First(i => i.Amount == 54000m);
        Assert.Equal("Failed", failedItem.Status);
    }

    [Fact]
    public async Task ExportOrganizationSalaries_ShouldProduceValidCsv()
    {
        await using var db = CreateDbContext();

        var org = new Organization("Salaries Export Corp", "sal_exp@gmail.com", "+2348055556666");
        db.Organizations.Add(org);

        var batch = PayrollBatch.Create(
            org.Id,
            Currency.NGN,
            PayrollSelectionMode.All,
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
            "initiator-1");
        db.PayrollBatches.Add(batch);

        var item = PayrollItem.Create(
            batch.Id,
            org.Id,
            "emp-01",
            "Diana Prince",
            "diana@example.com",
            Currency.NGN,
            70000m,
            7000m);
        item.Claim("worker-1");
        item.MarkCompleted(Guid.NewGuid(), Guid.NewGuid());
        db.PayrollItems.Add(item);

        await db.SaveChangesAsync();

        var handler = new ExportAdminOrganizationSalariesQueryHandler(db);
        var result = await handler.Handle(new ExportAdminOrganizationSalariesQuery(org.Id), CancellationToken.None);

        Assert.NotNull(result);
        var csvContent = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("63000.00", csvContent);
        Assert.Contains("Successfull", csvContent);
        Assert.Contains("May", csvContent);
    }

    [Fact]
    public async Task GetOrganizationSavings_ShouldReturnSavingsPlansAndAccounts()
    {
        await using var db = CreateDbContext();

        var org = new Organization("Savings Hub", "sav@gmail.com", "+2348066667777");
        db.Organizations.Add(org);

        var plan = SavingsPlan.CreateFixedLockPlan(
            org.Id,
            "admin-1",
            SavingsOwnerType.Organization,
            "Corporate Reserve Fund",
            "Annual reserve fund",
            Currency.NGN,
            0.12m,
            100000m,
            50000000m,
            30,
            365,
            1);
        db.SavingsPlans.Add(plan);

        var account = SavingsAccount.CreateFixedLockAccount(
            plan.Id,
            "user-1",
            org.Id,
            Currency.NGN,
            0.12m,
            1,
            365,
            DateTime.UtcNow);
        account.RecordContribution(4500000m, Guid.NewGuid(), "idemp-01");
        db.SavingsAccounts.Add(account);

        await db.SaveChangesAsync();

        var handler = new GetAdminOrganizationSavingsQueryHandler(db);
        var result = await handler.Handle(new GetAdminOrganizationSavingsQuery(org.Id), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal("Corporate Reserve Fund", item.Name);
        Assert.Equal(4500000m, item.CurrentAmount);
        Assert.Equal(12.0m, item.InterestRate);
        Assert.Equal("Active", item.Status);
    }

    [Fact]
    public async Task GetIndividualWalletsDirectory_ShouldReturnBalancesAndLoanObligations()
    {
        await using var db = CreateDbContext();

        var profile = new IndividualProfile("user-ind-1", "Michael", "Adejumo", avatarUrl: "https://api.dicebear.com/7.x/initials/svg?seed=MA");
        profile.SetKycStatus(KycStatus.Verified);
        db.IndividualProfiles.Add(profile);

        var wallet = Wallet.CreateIndividualWallet("user-ind-1", Currency.NGN);
        wallet.Credit(85000m);
        db.Wallets.Add(wallet);

        var app = LoanApplication.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user-ind-1",
            "Michael Adejumo",
            20000m,
            0.10m,
            6,
            3400m,
            1000m,
            21000m,
            80000m,
            0m,
            3400m,
            3400m,
            0.0425m,
            true,
            "Auto approved",
            RepaymentFrequency.Monthly,
            true);
        app.Approve("admin-1");
        var loan = LoanContract.CreateFromApplication(app);
        db.LoanContracts.Add(loan);

        await db.SaveChangesAsync();

        var identityService = Substitute.For<IIdentityService>();
        identityService.GetUserDetailsWithLockoutByIdsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, (string Email, string? PhoneNumber, bool IsLockedOut)>
            {
                ["user-ind-1"] = ("michael@example.com", "+2348077778888", false)
            });

        var handler = new GetAdminIndividualWalletsDirectoryQueryHandler(db, identityService);
        var result = await handler.Handle(new GetAdminIndividualWalletsDirectoryQuery(1, 10), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal(profile.Id, item.Id);
        Assert.Equal("Michael Adejumo", item.Name);
        Assert.Equal("https://api.dicebear.com/7.x/initials/svg?seed=MA", item.AvatarUrl);
        Assert.Equal(85000m, item.CurrentBalance);
        Assert.Equal(20000m, item.LoanRepayable);
        Assert.Equal("Active", item.Status);
    }

    [Fact]
    public async Task ExportIndividualWallets_ShouldProduceValidCsv()
    {
        await using var db = CreateDbContext();

        var profile = new IndividualProfile("user-ind-2", "Sarah", "Connor");
        profile.SetKycStatus(KycStatus.Verified);
        db.IndividualProfiles.Add(profile);

        var wallet = Wallet.CreateIndividualWallet("user-ind-2", Currency.NGN);
        wallet.Credit(95000m);
        db.Wallets.Add(wallet);

        await db.SaveChangesAsync();

        var identityService = Substitute.For<IIdentityService>();
        identityService.GetUserDetailsWithLockoutByIdsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, (string Email, string? PhoneNumber, bool IsLockedOut)>
            {
                ["user-ind-2"] = ("sarah@example.com", "+2348088889999", false)
            });

        var handler = new ExportAdminIndividualWalletsQueryHandler(db, identityService);
        var result = await handler.Handle(new ExportAdminIndividualWalletsQuery(), CancellationToken.None);

        Assert.NotNull(result);
        var csvContent = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("Sarah Connor", csvContent);
        Assert.Contains("95000.00", csvContent);
    }

    [Fact]
    public async Task GetAdminIndividualWalletsDirectory_WhenFilteringBySuspendedStatus_ShouldReturnOnlySuspendedProfiles()
    {
        await using var db = CreateDbContext();

        var activeProfile = new IndividualProfile("user-act-w", "Active", "User");
        activeProfile.SetKycStatus(KycStatus.Verified);

        var suspendedProfile = new IndividualProfile("user-susp-w", "Suspended", "User");
        suspendedProfile.SetKycStatus(KycStatus.Verified);
        suspendedProfile.Suspend("Risk review");

        db.IndividualProfiles.AddRange(activeProfile, suspendedProfile);

        var activeWallet = Wallet.CreateIndividualWallet("user-act-w", Currency.NGN);
        var suspendedWallet = Wallet.CreateIndividualWallet("user-susp-w", Currency.NGN);
        db.Wallets.AddRange(activeWallet, suspendedWallet);

        await db.SaveChangesAsync();

        var identityService = Substitute.For<IIdentityService>();
        identityService.GetUserDetailsWithLockoutByIdsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, (string Email, string? PhoneNumber, bool IsLockedOut)>
            {
                ["user-act-w"] = ("active@example.com", "+2348011112222", false),
                ["user-susp-w"] = ("suspended@example.com", "+2348033334444", false)
            });

        var handler = new GetAdminIndividualWalletsDirectoryQueryHandler(db, identityService);
        var result = await handler.Handle(new GetAdminIndividualWalletsDirectoryQuery(1, 10, Status: "Suspended"), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(suspendedProfile.Id, result.Items[0].Id);
        Assert.Equal("Suspended", result.Items[0].Status);
    }

    [Fact]
    public async Task ExportAdminIndividualWallets_WhenFilteringBySuspendedStatus_ShouldExportOnlySuspendedProfiles()
    {
        await using var db = CreateDbContext();

        var activeProfile = new IndividualProfile("user-act-expw", "Active", "WalletUser");
        activeProfile.SetKycStatus(KycStatus.Verified);

        var suspendedProfile = new IndividualProfile("user-susp-expw", "Suspended", "WalletUser");
        suspendedProfile.SetKycStatus(KycStatus.Verified);
        suspendedProfile.Suspend("Risk review");

        db.IndividualProfiles.AddRange(activeProfile, suspendedProfile);

        var activeWallet = Wallet.CreateIndividualWallet("user-act-expw", Currency.NGN);
        var suspendedWallet = Wallet.CreateIndividualWallet("user-susp-expw", Currency.NGN);
        db.Wallets.AddRange(activeWallet, suspendedWallet);

        await db.SaveChangesAsync();

        var identityService = Substitute.For<IIdentityService>();
        identityService.GetUserDetailsWithLockoutByIdsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, (string Email, string? PhoneNumber, bool IsLockedOut)>
            {
                ["user-act-expw"] = ("active@example.com", "+2348011112222", false),
                ["user-susp-expw"] = ("suspended@example.com", "+2348033334444", false)
            });

        var handler = new ExportAdminIndividualWalletsQueryHandler(db, identityService);
        var result = await handler.Handle(new ExportAdminIndividualWalletsQuery(Status: "Suspended"), CancellationToken.None);

        Assert.NotNull(result);
        var csvContent = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("Suspended WalletUser", csvContent);
        Assert.DoesNotContain("Active WalletUser", csvContent);
    }
}
