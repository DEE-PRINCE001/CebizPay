using CebizPay.Application.UseCases.Admin.Analytics;
using CebizPay.Application.UseCases.Admin.Dashboard;
using CebizPay.Application.UseCases.Admin.Treasury;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Savings.Entities;
using CebizPay.Domain.Savings.Enums;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Persistence.Queries;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.UnitTests.Admin;

public sealed class AdminDashboardAndTreasuryTests
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
    public async Task GetPlatformAdminMetrics_ShouldAccuratelyAggregateAllMetrics()
    {
        await using var db = CreateDbContext();

        // 1. Organizations: 2 active, 1 soft deleted
        var org1 = new Organization("Acme Corp", "acme@cebizpay.com", "+2348000000001");
        var org2 = new Organization("Beta Ltd", "beta@cebizpay.com", "+2348000000002");
        var org3 = new Organization("Deleted Org", "del@cebizpay.com", "+2348000000003");
        org3.SoftDelete();

        db.Organizations.AddRange(org1, org2, org3);

        // 2. Individual Profiles: 1 Pending KYC, 1 Verified, 1 Rejected
        var ind1 = new IndividualProfile("user-1", "John", "Doe");
        var ind2 = new IndividualProfile("user-2", "Jane", "Smith");
        ind2.SetKycStatus(KycStatus.Verified);
        var ind3 = new IndividualProfile("user-3", "Bob", "Brown");
        ind3.SetKycStatus(KycStatus.Rejected);

        db.IndividualProfiles.AddRange(ind1, ind2, ind3);

        // 3. Refresh Tokens: 
        // user-1 has 1 active token
        // user-2 has 2 active tokens (should count as 1 active user)
        // user-3 has 1 expired token
        // user-4 has 1 revoked token
        var now = DateTime.UtcNow;
        var token1 = new RefreshToken("user-1", "hash1", now.AddDays(15));
        var token2a = new RefreshToken("user-2", "hash2a", now.AddDays(10));
        var token2b = new RefreshToken("user-2", "hash2b", now.AddDays(20));
        var token3 = new RefreshToken("user-3", "hash3", now.AddDays(-1)); // expired
        var token4 = new RefreshToken("user-4", "hash4", now.AddDays(5));
        token4.Revoke(reason: "Logout"); // revoked

        db.RefreshTokens.AddRange(token1, token2a, token2b, token3, token4);

        // 4. Savings Accounts: 2 Active, 1 Pending
        var plan = SavingsPlan.CreateFixedLockPlan(null, "admin-1", SavingsOwnerType.Individual, "1-Yr Lock", null, Currency.NGN, 0.10m, 1000m, 100000m, 30, 365, 1);
        db.SavingsPlans.Add(plan);

        var acc1 = SavingsAccount.CreateFixedLockAccount(plan.Id, "user-1", null, Currency.NGN, 0.10m, 1, 30, now);
        acc1.RecordContribution(5000m, Guid.NewGuid(), "idemp-1"); // transitions to Active

        var acc2 = SavingsAccount.CreateFixedLockAccount(plan.Id, "user-2", null, Currency.NGN, 0.10m, 1, 60, now);
        acc2.RecordContribution(10000m, Guid.NewGuid(), "idemp-2"); // transitions to Active

        var acc3 = SavingsAccount.CreateFixedLockAccount(plan.Id, "user-3", null, Currency.NGN, 0.10m, 1, 90, now); // remains Pending

        db.SavingsAccounts.AddRange(acc1, acc2, acc3);

        await db.SaveChangesAsync();

        // Execute Handler
        var handler = new GetPlatformAdminMetricsQueryHandler(db);
        var result = await handler.Handle(new GetPlatformAdminMetricsQuery(), CancellationToken.None);

        Assert.Equal(2, result.TotalOrganizations);
        Assert.Equal(3, result.TotalIndividuals);
        Assert.Equal(1, result.PendingKycUsers);
        Assert.Equal(1, result.RejectedKycUsers);
        Assert.Equal(2, result.ActiveUsers); // user-1 and user-2
        Assert.Equal(2, result.ActiveSavingPlans); // acc1 and acc2
        Assert.True(result.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public async Task GetPlatformTreasurySummary_ShouldAccuratelyComputeBalancesAndReconciliation()
    {
        await using var db = CreateDbContext();

        // 1. Fee Revenue Account with credits and debits
        var feeAccount = LedgerAccount.CreateSystemAccount("NGN PLATFORM FEE", Currency.NGN, LedgerAccountType.FeeRevenue);
        db.LedgerAccounts.Add(feeAccount);

        var txn1 = new LedgerTransaction(LedgerTransactionType.PeerTransfer, "TXN-1");
        txn1.Complete(DateTime.UtcNow);
        var txn2 = new LedgerTransaction(LedgerTransactionType.PeerTransfer, "TXN-2");
        txn2.Complete(DateTime.UtcNow);
        db.LedgerTransactions.AddRange(txn1, txn2);

        var entry1 = new LedgerEntry(txn1.Id, feeAccount.Id, LedgerEntryDirection.Credit, 1000.00m, Currency.NGN, 1);
        var entry2 = new LedgerEntry(txn1.Id, feeAccount.Id, LedgerEntryDirection.Credit, 500.00m, Currency.NGN, 2);
        var entry3 = new LedgerEntry(txn2.Id, feeAccount.Id, LedgerEntryDirection.Debit, 200.00m, Currency.NGN, 1);
        db.LedgerEntries.AddRange(entry1, entry2, entry3);

        // 2. Pending Bank Transfers
        var pendingTransfer = BankTransfer.CreatePending(
            Guid.NewGuid(), Guid.NewGuid(), "058", "0123456789", "Recipient", 2500m, Currency.NGN, 50m, null, null, "REF-PENDING");
        db.BankTransfers.Add(pendingTransfer);

        // 3. Pending Funding Transaction
        var pendingFunding = FundingTransaction.Create(
            Guid.NewGuid(), null, PaymentProvider.Monnify, "PROV-TXN-1", FundingChannel.VirtualAccount, 1200m, Currency.NGN);
        db.FundingTransactions.Add(pendingFunding);

        // 4. Reconciliation Record
        var reconTime = DateTime.UtcNow.AddMinutes(-15);
        var recon = ReconciliationRecord.Create(ReconciliationType.BankTransfer, "REF-1", "Monnify");
        recon.MarkSuccess(1000m, "PROV-REF-1");
        // Update timestamps
        db.ReconciliationRecords.Add(recon);

        await db.SaveChangesAsync();

        // Execute Handler
        var handler = new GetPlatformTreasurySummaryQueryHandler(db);
        var result = await handler.Handle(new GetPlatformTreasurySummaryQuery(Currency.NGN), CancellationToken.None);

        Assert.Equal("NGN", result.Currency);
        Assert.Equal("₦", result.Symbol);
        Assert.Equal(1300.00m, result.AvailableBalance); // 1000 + 500 - 200
        Assert.Equal(3700.00m, result.PendingSettlement); // 2500 + 1200
        Assert.Equal(5000.00m, result.LedgerBalance);     // 1300 + 3700
        Assert.NotNull(result.LastReconciliationAt);
    }

    [Fact]
    public async Task PlatformAnalyticsQueryService_ShouldAggregateYearlyRevenueAndVolumeTimePoints()
    {
        await using var db = CreateDbContext();
        var year = 2026;

        // 1. Fee Revenue Account
        var feeAccount = LedgerAccount.CreateSystemAccount("NGN PLATFORM FEE", Currency.NGN, LedgerAccountType.FeeRevenue);
        db.LedgerAccounts.Add(feeAccount);

        // 2. Create transactions for Jan and Feb 2026
        var txnJan = new LedgerTransaction(LedgerTransactionType.BankTransfer, "TXN-JAN");
        txnJan.Complete(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc));

        var txnFeb = new LedgerTransaction(LedgerTransactionType.BankTransfer, "TXN-FEB");
        txnFeb.Complete(new DateTime(2026, 2, 20, 12, 0, 0, DateTimeKind.Utc));

        db.LedgerTransactions.AddRange(txnJan, txnFeb);

        // Jan Volume: 10,000, Fee: 100
        var entryJanVol = new LedgerEntry(txnJan.Id, Guid.NewGuid(), LedgerEntryDirection.Debit, 10000m, Currency.NGN, sequence: 1);
        var entryJanFee = new LedgerEntry(txnJan.Id, feeAccount.Id, LedgerEntryDirection.Credit, 100m, Currency.NGN, sequence: 2);

        // Feb Volume: 20,000, Fee: 250
        var entryFebVol = new LedgerEntry(txnFeb.Id, Guid.NewGuid(), LedgerEntryDirection.Debit, 20000m, Currency.NGN, sequence: 1);
        var entryFebFee = new LedgerEntry(txnFeb.Id, feeAccount.Id, LedgerEntryDirection.Credit, 250m, Currency.NGN, sequence: 2);

        db.LedgerEntries.AddRange(entryJanVol, entryJanFee, entryFebVol, entryFebFee);
        await db.SaveChangesAsync();

        // Execute Service
        var service = new PlatformAnalyticsQueryService(db);
        var result = await service.GetYearlyRevenueAnalyticsAsync(year, Currency.NGN, CancellationToken.None);

        Assert.Equal(2026, result.Year);
        Assert.Equal(350.00m, result.TotalRevenue);
        Assert.Equal(30000.00m, result.TotalTransactionVolume);
        Assert.Equal(12, result.MonthlyData.Count);

        var janData = result.MonthlyData[0];
        Assert.Equal("Jan", janData.Name);
        Assert.Equal(100.00m, janData.Revenue);
        Assert.Equal(10000.00m, janData.Volume);

        var febData = result.MonthlyData[1];
        Assert.Equal("Feb", febData.Name);
        Assert.Equal(250.00m, febData.Revenue);
        Assert.Equal(20000.00m, febData.Volume);

        var marData = result.MonthlyData[2];
        Assert.Equal("Mar", marData.Name);
        Assert.Equal(0m, marData.Revenue);
        Assert.Equal(0m, marData.Volume);
    }
}
