using System.Text;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Admin.Individuals;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Savings.Entities;
using CebizPay.Domain.Savings.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Admin;

public sealed class AdminIndividualsUseCasesTests
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
    public async Task GetIndividualsDirectory_ShouldReturnPaginatedResultsWithCompanyAndStatus()
    {
        await using var db = CreateDbContext();

        var prof1 = new IndividualProfile("user-01", "Johnson", "Mile", avatarUrl: "https://storage.cebizpay.com/avatars/user_01.jpg");
        var prof2 = new IndividualProfile("user-02", "Mike", "Johnson");
        prof2.SetKycStatus(KycStatus.Verified);
        prof1.UpdateProfessionalStatus(ProfessionalStatus.Staff);

        db.IndividualProfiles.AddRange(prof1, prof2);

        var org = new Organization("Cebis Company", "info@cebis.com", "+2348111111111");
        db.Organizations.Add(org);

        var membership = new OrganizationMembership("user-01", org.Id);
        db.OrganizationMemberships.Add(membership);

        await db.SaveChangesAsync();

        var identityService = Substitute.For<IIdentityService>();
        identityService.GetUserDetailsWithLockoutByIdsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, (string Email, string? PhoneNumber, bool IsLockedOut)>
            {
                ["user-01"] = ("Mile@gmail.com", "0815275927", true), // Suspended via lockout
                ["user-02"] = ("Mike@gmail.com", "0815275927", false)
            });

        var handler = new GetAdminIndividualsDirectoryQueryHandler(db, identityService);
        var query = new GetAdminIndividualsDirectoryQuery(1, 10);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);

        var item1 = result.Items.First(i => i.Id == prof1.Id);
        Assert.Equal("Johnson Mile", item1.Name);
        Assert.Equal("Mile@gmail.com", item1.Email);
        Assert.Equal("0815275927", item1.PhoneNumber);
        Assert.Equal("Staff", item1.ProfessionalStatus);
        Assert.Equal("Cebis Company", item1.CompanyName);
        Assert.Equal("Suspended", item1.Status);
        Assert.Equal("https://storage.cebizpay.com/avatars/user_01.jpg", item1.AvatarUrl);

        var item2 = result.Items.First(i => i.Id == prof2.Id);
        Assert.Equal("Mike Johnson", item2.Name);
        Assert.Equal("Not-a-Staff", item2.ProfessionalStatus);
        Assert.Equal("None", item2.CompanyName);
        Assert.Equal("Verified", item2.Status);
    }

    [Fact]
    public async Task GetIndividualDetails_ShouldReturnCredentialsAndProfile()
    {
        await using var db = CreateDbContext();

        var prof = new IndividualProfile("user-01", "Mike", "Johnson", avatarUrl: "https://storage.cebizpay.com/photos/mike_johnson.jpg");
        prof.SetKycStatus(KycStatus.Verified);
        prof.UpdateProfessionalStatus(ProfessionalStatus.Staff);
        db.IndividualProfiles.Add(prof);

        var org = new Organization("Cebis Company", "info@cebis.com", "+2348111111111");
        db.Organizations.Add(org);
        db.OrganizationMemberships.Add(new OrganizationMembership("user-01", org.Id));

        var doc = new KycDocument("user-01", DocumentType.Nimc, "12345678901", "https://storage.cebizpay.com/docs/nin_card.pdf");
        db.KycDocuments.Add(doc);

        await db.SaveChangesAsync();

        var identityService = Substitute.For<IIdentityService>();
        identityService.GetUserDetailsWithLockoutByIdsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, (string Email, string? PhoneNumber, bool IsLockedOut)>
            {
                ["user-01"] = ("Mike@gmail.com", "0815275927", false)
            });

        var handler = new GetAdminIndividualDetailsQueryHandler(db, identityService);
        var query = new GetAdminIndividualDetailsQuery(prof.Id.ToString());

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Mike Johnson", result.Name);
        Assert.Equal("Active", result.Status);
        Assert.Equal("Staff", result.ProfessionalStatus);
        Assert.Equal("Cebis Company", result.CompanyName);
        Assert.Equal("https://storage.cebizpay.com/photos/mike_johnson.jpg", result.PhotoUrl);
        Assert.Single(result.Credentials);

        var cred = result.Credentials[0];
        Assert.Equal("National Identity Card", cred.Title);
        Assert.Equal("Nimc", cred.DocumentType);
        Assert.Equal("12345678901", cred.DocumentNumber);
        Assert.Equal("https://storage.cebizpay.com/docs/nin_card.pdf", cred.FileUrl);
    }

    [Fact]
    public async Task GetIndividualWallet_ShouldReturnWalletAndVirtualAccountOverview()
    {
        await using var db = CreateDbContext();

        var prof = new IndividualProfile("user-01", "Mike", "Johnson");
        prof.SetKycStatus(KycStatus.Verified);
        db.IndividualProfiles.Add(prof);

        var wallet = Wallet.CreateIndividualWallet("user-01", Currency.NGN);
        wallet.Credit(450000m);
        db.Wallets.Add(wallet);

        var virtualAccount = VirtualAccount.CreateIndividual(
            "user-01", PaymentProvider.Flutterwave, "0123456789", "Mike Johnson", "035", "Wema Bank / CebizPay", Currency.NGN);
        db.VirtualAccounts.Add(virtualAccount);

        await db.SaveChangesAsync();

        var handler = new GetAdminIndividualWalletQueryHandler(db);
        var query = new GetAdminIndividualWalletQuery(prof.Id.ToString());

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(450000m, result.AvailableBalance);
        Assert.Equal(450000m, result.LedgerBalance);
        Assert.Equal("NGN", result.Currency);
        Assert.Equal(2, result.Tier);
        Assert.Equal("0123456789", result.VirtualAccountNumber);
        Assert.Equal("Wema Bank / CebizPay", result.BankName);
        Assert.Equal("Active", result.Status);
    }

    [Fact]
    public async Task GetIndividualSavings_ShouldReturnSavingsPlanItems()
    {
        await using var db = CreateDbContext();

        var prof = new IndividualProfile("user-01", "Mike", "Johnson");
        db.IndividualProfiles.Add(prof);

        var plan = SavingsPlan.CreateFixedLockPlan(
            null, "admin-1", SavingsOwnerType.Individual, "Target Savings - New Car", "Goal car savings", Currency.NGN, 0.125m, 1000m, 5000000m, 30, 365, 1);
        db.SavingsPlans.Add(plan);

        var savingsAccount = SavingsAccount.CreateGoalBasedAccount(
            plan.Id, "user-01", null, Currency.NGN, 2000000.00m, 50000m, SavingsContributionFrequency.Monthly,
            0.125m, 1, DateTime.UtcNow, DateTime.UtcNow.AddYears(1));
        savingsAccount.RecordContribution(650000m, Guid.NewGuid(), "contrib-01");
        db.SavingsAccounts.Add(savingsAccount);

        await db.SaveChangesAsync();

        var handler = new GetAdminIndividualSavingsQueryHandler(db);
        var query = new GetAdminIndividualSavingsQuery(prof.Id.ToString());

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);

        var item = result.Items[0];
        Assert.Equal("Target Savings - New Car", item.Name);
        Assert.Equal(650000m, item.CurrentAmount);
        Assert.Equal(12.5m, item.InterestRate);
    }

    [Fact]
    public async Task ExportIndividuals_ShouldReturnValidCsvStream()
    {
        await using var db = CreateDbContext();

        var prof = new IndividualProfile("user-01", "Johnson", "Mile");
        db.IndividualProfiles.Add(prof);
        await db.SaveChangesAsync();

        var identityService = Substitute.For<IIdentityService>();
        identityService.GetUserDetailsWithLockoutByIdsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, (string Email, string? PhoneNumber, bool IsLockedOut)>
            {
                ["user-01"] = ("Mile@gmail.com", "0815275927", false)
            });

        var handler = new ExportAdminIndividualsQueryHandler(db, identityService);
        var query = new ExportAdminIndividualsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("text/csv; charset=utf-8", result.ContentType);
        Assert.Equal("individuals_export.csv", result.FileName);

        var csv = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("Individual ID,Full Name,Email,Phone Number,Professional Status,Company Name,Status,Created At (UTC)", csv);
        Assert.Contains("Johnson Mile", csv);
        Assert.Contains("Mile@gmail.com", csv);
    }
}
