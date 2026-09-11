using System.Text;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Admin.Organizations;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Admin;

public sealed class AdminOrganizationsUseCasesTests
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
    public async Task GetOrganizationsDirectory_ShouldReturnPaginatedResultsAndStaffCounts()
    {
        await using var db = CreateDbContext();

        var org1 = new Organization("Cebis Tech", "cebistech@gmail.com", "+2348011112222", "Technology", "Abuja Obanikoro");
        var org2 = new Organization("Apex Finance", "apex@gmail.com", "+2348022223333", "Finance", "Lagos Island");
        org2.TransitionStatus(OrganizationStatus.Verified);

        db.Organizations.AddRange(org1, org2);

        var member1 = new OrganizationMembership("user-1", org1.Id);
        var member2 = new OrganizationMembership("user-2", org1.Id);
        var terminated = new OrganizationMembership("user-3", org1.Id);
        terminated.TerminateWorkAccess("Left company");

        db.OrganizationMemberships.AddRange(member1, member2, terminated);
        await db.SaveChangesAsync();

        var handler = new GetAdminOrganizationsDirectoryQueryHandler(db);
        var query = new GetAdminOrganizationsDirectoryQuery(1, 10);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);

        var cebisTech = result.Items.First(i => i.Id == org1.Id);
        Assert.Equal("Cebis Tech", cebisTech.Name);
        Assert.Equal("Technology", cebisTech.Category);
        Assert.Equal("Abuja Obanikoro", cebisTech.Address);
        Assert.Equal("Pending", cebisTech.Status);
        Assert.Equal(2, cebisTech.StaffCount); // terminated excluded

        var apex = result.Items.First(i => i.Id == org2.Id);
        Assert.Equal("Verified", apex.Status);
        Assert.Equal(0, apex.StaffCount);
    }

    [Fact]
    public async Task GetOrganizationsDirectory_WithSearchAndFilter_ShouldFilterCorrectly()
    {
        await using var db = CreateDbContext();

        var org1 = new Organization("Cebis Tech", "cebistech@gmail.com", "+2348011112222", "Technology", "Abuja Obanikoro");
        var org2 = new Organization("Apex Finance", "apex@gmail.com", "+2348022223333", "Finance", "Lagos Island");
        db.Organizations.AddRange(org1, org2);
        await db.SaveChangesAsync();

        var handler = new GetAdminOrganizationsDirectoryQueryHandler(db);

        // Search by address
        var querySearch = new GetAdminOrganizationsDirectoryQuery(Search: "Obanikoro");
        var resultSearch = await handler.Handle(querySearch, CancellationToken.None);
        Assert.Single(resultSearch.Items);
        Assert.Equal(org1.Id, resultSearch.Items[0].Id);

        // Filter by category
        var queryCat = new GetAdminOrganizationsDirectoryQuery(Category: "Finance");
        var resultCat = await handler.Handle(queryCat, CancellationToken.None);
        Assert.Single(resultCat.Items);
        Assert.Equal(org2.Id, resultCat.Items[0].Id);

        // Filter by status
        var queryStatus = new GetAdminOrganizationsDirectoryQuery(Status: "Pending");
        var resultStatus = await handler.Handle(queryStatus, CancellationToken.None);
        Assert.Equal(2, resultStatus.TotalCount);
    }

    [Fact]
    public async Task GetOrganizationDetails_WhenExists_ShouldReturnDetailsWithCredentials()
    {
        await using var db = CreateDbContext();

        var org = new Organization("Cebis Tech", "cebistech@gmail.com", "+2348011112222", "Technology", "Abuja Obanikoro");
        org.CompleteStep2("RC-998877", "https://logo.url/cebis.png", "https://storage.cebizpay.com/docs/cac_cebis_tech.pdf");
        db.Organizations.Add(org);

        var member = new OrganizationMembership("user-1", org.Id);
        db.OrganizationMemberships.Add(member);
        await db.SaveChangesAsync();

        var handler = new GetAdminOrganizationDetailsQueryHandler(db);
        var query = new GetAdminOrganizationDetailsQuery(org.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(org.Id, result.Id);
        Assert.Equal("Cebis Tech", result.Name);
        Assert.Equal("Technology", result.Category);
        Assert.Equal(1, result.StaffCount);
        Assert.Single(result.Credentials);
        Assert.Equal("CAC_CERTIFICATE", result.Credentials[0].DocumentType);
        Assert.Equal("https://storage.cebizpay.com/docs/cac_cebis_tech.pdf", result.Credentials[0].FileUrl);
    }

    [Fact]
    public async Task GetOrganizationDetails_WhenNotFound_ShouldReturnNull()
    {
        await using var db = CreateDbContext();
        var handler = new GetAdminOrganizationDetailsQueryHandler(db);
        var query = new GetAdminOrganizationDetailsQuery(Guid.NewGuid());

        var result = await handler.Handle(query, CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrganizationStaffRoster_ShouldReturnStaffWithWalletsAndVirtualAccounts()
    {
        await using var db = CreateDbContext();

        var org = new Organization("Cebis Tech", "cebistech@gmail.com", "+2348011112222");
        db.Organizations.Add(org);

        var salLevel = new SalaryLevel(org.Id, "Senior Engineer", 500000m, "NGN");
        db.SalaryLevels.Add(salLevel);

        var membership = new OrganizationMembership("user-mike", org.Id, MembershipRoleType.Member, salaryLevelId: salLevel.Id);
        db.OrganizationMemberships.Add(membership);

        var profile = new IndividualProfile("user-mike", "Johnson", "Mike");
        profile.SetKycStatus(KycStatus.Verified);
        db.IndividualProfiles.Add(profile);

        var wallet = Wallet.CreateIndividualWallet("user-mike", Currency.NGN);
        db.Wallets.Add(wallet);

        var va = VirtualAccount.CreateIndividual(
            "user-mike",
            PaymentProvider.Paystack,
            "02826893",
            "Johnson Mike",
            "044",
            "Access Bank",
            Currency.NGN,
            "ref-123");
        db.VirtualAccounts.Add(va);

        await db.SaveChangesAsync();

        var identityService = Substitute.For<IIdentityService>();
        identityService.GetUserDetailsByIdsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, (string Email, string? PhoneNumber)>
            {
                ["user-mike"] = ("Mike@gmail.com", "+2348099990000")
            });

        var handler = new GetAdminOrganizationStaffRosterQueryHandler(db, identityService);
        var query = new GetAdminOrganizationStaffRosterQuery(org.Id, 1, 10);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        var staff = result.Items[0];
        Assert.Equal(membership.Id.ToString(), staff.Id);
        Assert.Equal("Johnson Mike", staff.Name);
        Assert.Equal("Mike@gmail.com", staff.Email);
        Assert.Equal(wallet.Id.ToString(), staff.WalletId);
        Assert.Contains("02826893", staff.BankAccount);
        Assert.Contains("Access Bank", staff.BankAccount);
        Assert.Equal("Verified", staff.Status);
        Assert.Equal("500,000.00", staff.MonthlySalary);
    }

    [Fact]
    public async Task GetOrganizationStaffRoster_WhenOrgNotFound_ShouldThrowKeyNotFoundException()
    {
        await using var db = CreateDbContext();
        var identityService = Substitute.For<IIdentityService>();
        var handler = new GetAdminOrganizationStaffRosterQueryHandler(db, identityService);
        var query = new GetAdminOrganizationStaffRosterQuery(Guid.NewGuid(), 1, 10);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrganizationDocuments_WhenExists_ShouldReturnDocuments()
    {
        await using var db = CreateDbContext();

        var org = new Organization("Cebis Tech", "cebistech@gmail.com", "+2348011112222");
        org.CompleteStep2("RC-12345", "https://logo.png", "https://cebizpay-storage.s3.amazonaws.com/kyb/cac_certificate.pdf");
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var handler = new GetAdminOrganizationDocumentsQueryHandler(db);
        var query = new GetAdminOrganizationDocumentsQuery(org.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(org.Id, result.OrganizationId);
        Assert.Single(result.Documents);
        Assert.Equal("CAC_CERTIFICATE", result.Documents[0].DocumentType);
        Assert.Equal("https://cebizpay-storage.s3.amazonaws.com/kyb/cac_certificate.pdf", result.Documents[0].FileUrl);
    }

    [Fact]
    public async Task ExportOrganizations_ShouldReturnValidCsvContent()
    {
        await using var db = CreateDbContext();

        var org1 = new Organization("Cebis Tech, LLC", "cebis@tech.com", "+2348011112222", "Technology", "Abuja");
        var org2 = new Organization("Beta Corp", "beta@corp.com", "+2348022223333", "Finance", "Lagos");
        db.Organizations.AddRange(org1, org2);
        await db.SaveChangesAsync();

        var handler = new ExportAdminOrganizationsQueryHandler(db);
        var query = new ExportAdminOrganizationsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal("text/csv", result.ContentType);
        Assert.Equal("organizations_export.csv", result.FileName);

        var csv = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("Organization ID,Company Name,Category,Email,Phone,Address,Status,KYB Status,Staff Count,CAC Number,Created At (UTC)", csv);
        Assert.Contains("\"Cebis Tech, LLC\"", csv); // escaped quotes for comma
        Assert.Contains("Beta Corp", csv);
    }
}
