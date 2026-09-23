using System.Text;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Admin.Individuals;
using CebizPay.Application.UseCases.Individuals.GetKycDocuments;
using CebizPay.Application.UseCases.Individuals.UpdateKycStatus;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
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
    public async Task GetIndividualDetails_WhenProfileIsSuspended_ShouldReturnSuspensionFieldsAndStatus()
    {
        await using var db = CreateDbContext();

        var prof = new IndividualProfile("user-susp", "Sarah", "Connor");
        prof.SetKycStatus(KycStatus.Verified);
        prof.Suspend("Suspicious transactions flagged");
        db.IndividualProfiles.Add(prof);
        await db.SaveChangesAsync();

        var identityService = Substitute.For<IIdentityService>();
        identityService.GetUserDetailsWithLockoutByIdsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, (string Email, string? PhoneNumber, bool IsLockedOut)>
            {
                ["user-susp"] = ("sarah@example.com", "08123456789", false)
            });

        var handler = new GetAdminIndividualDetailsQueryHandler(db, identityService);
        var query = new GetAdminIndividualDetailsQuery(prof.Id.ToString());

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Suspended", result.Status);
        Assert.True(result.IsSuspended);
        Assert.Equal("Suspicious transactions flagged", result.SuspensionReason);
        Assert.NotNull(result.SuspendedAtUtc);
    }

    [Fact]
    public async Task GetIndividualsDirectory_WhenFilteringBySuspendedStatus_ShouldReturnOnlySuspendedProfiles()
    {
        await using var db = CreateDbContext();

        var activeProf = new IndividualProfile("user-act", "Active", "User");
        activeProf.SetKycStatus(KycStatus.Verified);

        var suspendedProf = new IndividualProfile("user-susp", "Suspended", "User");
        suspendedProf.SetKycStatus(KycStatus.Verified);
        suspendedProf.Suspend("Violation of terms");

        db.IndividualProfiles.AddRange(activeProf, suspendedProf);
        await db.SaveChangesAsync();

        var identityService = Substitute.For<IIdentityService>();
        identityService.GetUserDetailsWithLockoutByIdsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, (string Email, string? PhoneNumber, bool IsLockedOut)>
            {
                ["user-act"] = ("active@example.com", "08111111111", false),
                ["user-susp"] = ("susp@example.com", "08222222222", false)
            });

        var handler = new GetAdminIndividualsDirectoryQueryHandler(db, identityService);
        var query = new GetAdminIndividualsDirectoryQuery(1, 10, Status: "Suspended");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(suspendedProf.Id, result.Items[0].Id);
        Assert.Equal("Suspended", result.Items[0].Status);
    }

    [Fact]
    public async Task ExportAdminIndividuals_WhenFilteringBySuspendedStatus_ShouldExportOnlySuspendedProfiles()
    {
        await using var db = CreateDbContext();

        var activeProf = new IndividualProfile("user-act-exp", "Active", "ExportUser");
        activeProf.SetKycStatus(KycStatus.Verified);

        var suspendedProf = new IndividualProfile("user-susp-exp", "Suspended", "ExportUser");
        suspendedProf.SetKycStatus(KycStatus.Verified);
        suspendedProf.Suspend("Export suspension test");

        db.IndividualProfiles.AddRange(activeProf, suspendedProf);
        await db.SaveChangesAsync();

        var identityService = Substitute.For<IIdentityService>();
        identityService.GetUserDetailsWithLockoutByIdsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, (string Email, string? PhoneNumber, bool IsLockedOut)>
            {
                ["user-act-exp"] = ("actexp@example.com", "08111111111", false),
                ["user-susp-exp"] = ("suspexp@example.com", "08222222222", false)
            });

        var handler = new ExportAdminIndividualsQueryHandler(db, identityService);
        var query = new ExportAdminIndividualsQuery(Status: "Suspended");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        var csv = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("Suspended ExportUser", csv);
        Assert.DoesNotContain("Active ExportUser", csv);
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

    [Fact]
    public async Task UpdateKycStatus_WhenProfileIdPassed_SuccessfullyResolvesAndUpdatesStatus()
    {
        await using var db = CreateDbContext();

        var admin = new AdminProfile("admin-user-1", AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(admin);

        var prof = new IndividualProfile("user-01", "Mike", "Johnson");
        db.IndividualProfiles.Add(prof);

        var doc = new KycDocument("user-01", DocumentType.Nimc, "12345678901", "https://storage.cebizpay.com/docs/nin.pdf");
        db.KycDocuments.Add(doc);
        await db.SaveChangesAsync();

        var eventPublisher = Substitute.For<IEventPublisher>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("admin-user-1");

        var handler = new UpdateKycStatusCommandHandler(db, eventPublisher, currentUserService);

        // Pass the profile GUID ID (as frontend does)
        var command = new UpdateKycStatusCommand(prof.Id.ToString(), KycStatus.Verified, "admin-user-1", "Approved");
        var response = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("user-01", response.UserId);
        Assert.Equal("Verified", response.KycStatus);

        var reloadedProfile = await db.IndividualProfiles.FirstAsync(p => p.Id == prof.Id);
        Assert.Equal(KycStatus.Verified, reloadedProfile.KycStatus);

        var reloadedDoc = await db.KycDocuments.FirstAsync(d => d.Id == doc.Id);
        Assert.Equal(KycStatus.Verified, reloadedDoc.Status);
    }

    [Fact]
    public async Task UpdateKycStatus_WhenUserIdPassed_SuccessfullyResolvesAndUpdatesStatus()
    {
        await using var db = CreateDbContext();

        var admin = new AdminProfile("admin-user-1", AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(admin);

        var prof = new IndividualProfile("user-02", "Jane", "Doe");
        db.IndividualProfiles.Add(prof);
        await db.SaveChangesAsync();

        var eventPublisher = Substitute.For<IEventPublisher>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("admin-user-1");

        var handler = new UpdateKycStatusCommandHandler(db, eventPublisher, currentUserService);

        // Pass the Identity User ID string
        var command = new UpdateKycStatusCommand("user-02", KycStatus.Verified, "admin-user-1", "Approved");
        var response = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("user-02", response.UserId);
        Assert.Equal("Verified", response.KycStatus);

        var reloadedProfile = await db.IndividualProfiles.FirstAsync(p => p.Id == prof.Id);
        Assert.Equal(KycStatus.Verified, reloadedProfile.KycStatus);
    }

    [Fact]
    public async Task GetKycDocuments_WhenProfileIdPassed_SuccessfullyResolvesAndReturnsDocuments()
    {
        await using var db = CreateDbContext();

        var admin = new AdminProfile("admin-user-1", AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(admin);

        var prof = new IndividualProfile("user-01", "Mike", "Johnson");
        db.IndividualProfiles.Add(prof);

        var doc = new KycDocument("user-01", DocumentType.Nimc, "12345678901", "https://storage.cebizpay.com/docs/nin.pdf");
        db.KycDocuments.Add(doc);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("admin-user-1");

        var handler = new GetKycDocumentsQueryHandler(db, currentUserService);

        // Query using Profile GUID
        var query = new GetKycDocumentsQuery(prof.Id.ToString());
        var docs = (await handler.Handle(query, CancellationToken.None)).ToList();

        Assert.Single(docs);
        Assert.Equal(doc.Id, docs[0].Id);
        Assert.Equal("user-01", docs[0].UserId);
        Assert.Equal("Nimc", docs[0].DocumentType);
    }

    [Fact]
    public async Task SuspendIndividual_AsSuperAdmin_ShouldSuspendProfile_AndPublishEvent()
    {
        await using var db = CreateDbContext();

        var admin = new AdminProfile("admin-user-1", AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(admin);

        var prof = new IndividualProfile("user-01", "Mike", "Johnson");
        db.IndividualProfiles.Add(prof);
        await db.SaveChangesAsync();

        var eventPublisher = Substitute.For<IEventPublisher>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("admin-user-1");

        var handler = new SuspendIndividualCommandHandler(db, eventPublisher, currentUserService);
        var command = new SuspendIndividualCommand(prof.Id.ToString(), "Suspicious transactional activity", "admin-user-1");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsSuspended);
        Assert.Equal("Suspended", result.Status);
        Assert.Equal("Suspicious transactional activity", result.SuspensionReason);
        Assert.NotNull(result.SuspendedAtUtc);

        var reloaded = await db.IndividualProfiles.FirstAsync(p => p.Id == prof.Id);
        Assert.True(reloaded.IsSuspended);
        Assert.Equal("Suspicious transactional activity", reloaded.SuspensionReason);

        await eventPublisher.Received(1).PublishAsync(
            Arg.Is<IndividualSuspendedDomainEvent>(e =>
                e.ProfileId == prof.Id &&
                e.UserId == "user-01" &&
                e.Reason == "Suspicious transactional activity" &&
                e.AdminUserId == "admin-user-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuspendIndividual_SelfSuspension_ShouldThrowInvalidOperationException()
    {
        await using var db = CreateDbContext();

        var admin = new AdminProfile("same-user-id", AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(admin);

        var prof = new IndividualProfile("same-user-id", "Admin", "Person");
        db.IndividualProfiles.Add(prof);
        await db.SaveChangesAsync();

        var eventPublisher = Substitute.For<IEventPublisher>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("same-user-id");

        var handler = new SuspendIndividualCommandHandler(db, eventPublisher, currentUserService);
        var command = new SuspendIndividualCommand(prof.Id.ToString(), "Self suspension attempt", "same-user-id");

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task SuspendIndividual_NonAdmin_ShouldThrowUnauthorizedAccessException()
    {
        await using var db = CreateDbContext();

        var prof = new IndividualProfile("user-01", "Mike", "Johnson");
        db.IndividualProfiles.Add(prof);
        await db.SaveChangesAsync();

        var eventPublisher = Substitute.For<IEventPublisher>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("regular-user");

        var handler = new SuspendIndividualCommandHandler(db, eventPublisher, currentUserService);
        var command = new SuspendIndividualCommand(prof.Id.ToString(), "Unauthorized suspension", "regular-user");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task ReactivateIndividual_WhenSuspended_ShouldReactivateProfile_AndPublishEvent()
    {
        await using var db = CreateDbContext();

        var admin = new AdminProfile("admin-user-1", AdminRoleType.Admin);
        db.AdminProfiles.Add(admin);

        var prof = new IndividualProfile("user-01", "Mike", "Johnson");
        prof.SetKycStatus(KycStatus.Verified);
        prof.Suspend("Pending regulatory check");
        db.IndividualProfiles.Add(prof);
        await db.SaveChangesAsync();

        var eventPublisher = Substitute.For<IEventPublisher>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("admin-user-1");

        var handler = new ReactivateIndividualCommandHandler(db, eventPublisher, currentUserService);
        var command = new ReactivateIndividualCommand(prof.Id.ToString(), "Regulatory check cleared", "admin-user-1");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result.IsSuspended);
        Assert.Equal("Active", result.Status);
        Assert.Null(result.SuspensionReason);
        Assert.Null(result.SuspendedAtUtc);

        var reloaded = await db.IndividualProfiles.FirstAsync(p => p.Id == prof.Id);
        Assert.False(reloaded.IsSuspended);
        Assert.Null(reloaded.SuspensionReason);

        await eventPublisher.Received(1).PublishAsync(
            Arg.Is<IndividualReactivatedDomainEvent>(e =>
                e.ProfileId == prof.Id &&
                e.UserId == "user-01" &&
                e.Reason == "Regulatory check cleared" &&
                e.AdminUserId == "admin-user-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReactivateIndividual_WhenNotSuspended_ShouldThrowInvalidOperationException()
    {
        await using var db = CreateDbContext();

        var admin = new AdminProfile("admin-user-1", AdminRoleType.Admin);
        db.AdminProfiles.Add(admin);

        var prof = new IndividualProfile("user-01", "Mike", "Johnson");
        db.IndividualProfiles.Add(prof);
        await db.SaveChangesAsync();

        var eventPublisher = Substitute.For<IEventPublisher>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("admin-user-1");

        var handler = new ReactivateIndividualCommandHandler(db, eventPublisher, currentUserService);
        var command = new ReactivateIndividualCommand(prof.Id.ToString(), "Reactivate unsuspended", "admin-user-1");

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task GetAdminIndividualTransactions_VirtualAccountDepositWithSenderDetails_ReturnsAccurateDisplayFields()
    {
        await using var db = CreateDbContext();

        var profile = new IndividualProfile("user-dep-01", "Sunday", "Bello");
        db.IndividualProfiles.Add(profile);

        var wallet = Wallet.CreateIndividualWallet("user-dep-01", Currency.NGN);
        db.Wallets.Add(wallet);

        var ledgerAcc = LedgerAccount.CreateWalletAccount(wallet.Id, "Sunday Bello Wallet", Currency.NGN);
        db.LedgerAccounts.Add(ledgerAcc);

        var extAcc = ExternalFundingAccount.Create(wallet.Id, PaymentProvider.Monnify, "7820987654", "Sunday Bello", "035", "Wema Bank", Currency.NGN, isPrimary: true);
        db.ExternalFundingAccounts.Add(extAcc);

        var ledgerTx = new LedgerTransaction(LedgerTransactionType.VirtualAccountDeposit, "FND-MNFY|11|20260921174022|000603", null, "Inbound deposit");
        ledgerTx.Complete(DateTime.UtcNow);
        db.LedgerTransactions.Add(ledgerTx);

        var entry = new LedgerEntry(ledgerTx.Id, ledgerAcc.Id, LedgerEntryDirection.Credit, 4000m, Currency.NGN, 1);
        db.LedgerEntries.Add(entry);

        var fundingTx = FundingTransaction.CreateWithExternalAccount(
            wallet.Id, extAcc.Id, PaymentProvider.Monnify, "MNFY|11|20260921174022|000603", "evt_100", FundingChannel.VirtualAccount,
            4000m, 0m, 4000m, 0m, null, null, null, Currency.NGN);
        fundingTx.MarkCompleted(ledgerTx.Id);
        fundingTx.SetSenderDetails("ADEKUNLE SAMUEL ADENIRAN", "0123456789", "058", "Guaranty Trust Bank");
        db.FundingTransactions.Add(fundingTx);

        await db.SaveChangesAsync();

        var handler = new GetAdminIndividualTransactionsQueryHandler(db);
        var query = new GetAdminIndividualTransactionsQuery(profile.Id.ToString());

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal("FND-MNFY|11|20260921174022|000603", item.Id);
        Assert.Equal("Bank Deposit", item.TransactionType);
        Assert.Equal("ADEKUNLE SAMUEL ADENIRAN", item.CounterpartyName);
        Assert.Equal("Bank Transfer", item.Method);
        Assert.Equal("0123456789", item.AccountOrWalletId);
        Assert.Equal("FND-MNFY|11|20260921174022|000603", item.ReceiverSenderId);
        Assert.Equal("Successfull", item.Status);
    }

    [Fact]
    public async Task GetAdminIndividualTransactions_PeerTransfer_ReturnsAccurateTransferDirectionAndCounterparty()
    {
        await using var db = CreateDbContext();

        var senderProf = new IndividualProfile("sender-user", "Alice", "Smith");
        var receiverProf = new IndividualProfile("receiver-user", "Bob", "Jones");
        db.IndividualProfiles.AddRange(senderProf, receiverProf);

        var senderWallet = Wallet.CreateIndividualWallet("sender-user", Currency.NGN);
        var receiverWallet = Wallet.CreateIndividualWallet("receiver-user", Currency.NGN);
        db.Wallets.AddRange(senderWallet, receiverWallet);

        var senderAcc = LedgerAccount.CreateWalletAccount(senderWallet.Id, "Alice Wallet", Currency.NGN);
        var receiverAcc = LedgerAccount.CreateWalletAccount(receiverWallet.Id, "Bob Wallet", Currency.NGN);
        db.LedgerAccounts.AddRange(senderAcc, receiverAcc);

        var ledgerTx = new LedgerTransaction(LedgerTransactionType.PeerTransfer, "TX-P2P-12345", null, "P2P transfer");
        ledgerTx.Complete(DateTime.UtcNow);
        db.LedgerTransactions.Add(ledgerTx);

        var debitEntry = new LedgerEntry(ledgerTx.Id, senderAcc.Id, LedgerEntryDirection.Debit, 5000m, Currency.NGN, 1);
        var creditEntry = new LedgerEntry(ledgerTx.Id, receiverAcc.Id, LedgerEntryDirection.Credit, 5000m, Currency.NGN, 2);
        db.LedgerEntries.AddRange(debitEntry, creditEntry);

        await db.SaveChangesAsync();

        var handler = new GetAdminIndividualTransactionsQueryHandler(db);
        var query = new GetAdminIndividualTransactionsQuery(senderProf.Id.ToString());

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal("Transfer Out", item.TransactionType);
        Assert.Equal("Bob Jones", item.CounterpartyName);
        Assert.Equal("Wallet ID", item.Method);
        Assert.Equal(receiverWallet.Id.ToString("N")[..12], item.AccountOrWalletId);
        Assert.Equal("receiver-user", item.ReceiverSenderId);
        Assert.Equal("Successfull", item.Status);
    }
}
