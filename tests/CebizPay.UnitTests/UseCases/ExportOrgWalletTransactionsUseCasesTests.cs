using System.Text;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Organizations.Wallet;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.UseCases;

public sealed class ExportOrgWalletTransactionsUseCasesTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void Validator_WhenOrganizationIdEmpty_ShouldFail()
    {
        var validator = new ExportOrgWalletTransactionsQueryValidator();
        var query = new ExportOrgWalletTransactionsQuery(Guid.Empty);

        var result = validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(query.OrganizationId));
    }

    [Fact]
    public async Task Handle_WhenAuthorized_GeneratesValidCsvStreamWithEntries()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var org = new Organization("Cebis Tech", "admin@cebistech.com", "+2348011223344");
        dbContext.Organizations.Add(org);

        var wallet = Wallet.CreateOrganizationWallet(org.Id, Currency.NGN);
        dbContext.Wallets.Add(wallet);

        var orgLedger = LedgerAccount.CreateWalletAccount(wallet.Id, "Cebis Org Wallet", Currency.NGN);
        dbContext.LedgerAccounts.Add(orgLedger);

        var bankSettlementLedger = LedgerAccount.CreateSystemAccount("Bank Settlement", Currency.NGN, LedgerAccountType.SystemSettlement);
        dbContext.LedgerAccounts.Add(bankSettlementLedger);

        var tx1 = new LedgerTransaction(LedgerTransactionType.BankTransfer, "REF-TXN-001", null, "Vendor Disbursement");
        tx1.Complete(DateTime.UtcNow);
        dbContext.LedgerTransactions.Add(tx1);

        var entry1Org = new LedgerEntry(tx1.Id, orgLedger.Id, LedgerEntryDirection.Debit, 25000.50m, Currency.NGN, 1);
        var entry1Counter = new LedgerEntry(tx1.Id, bankSettlementLedger.Id, LedgerEntryDirection.Credit, 25000.50m, Currency.NGN, 2);
        dbContext.LedgerEntries.AddRange(entry1Org, entry1Counter);

        var tx2 = new LedgerTransaction(LedgerTransactionType.VirtualAccountDeposit, "REF-TXN-002", null, "Wallet Inflow");
        tx2.Complete(DateTime.UtcNow);
        dbContext.LedgerTransactions.Add(tx2);

        var entry2Org = new LedgerEntry(tx2.Id, orgLedger.Id, LedgerEntryDirection.Credit, 100000m, Currency.NGN, 1);
        var entry2Counter = new LedgerEntry(tx2.Id, bankSettlementLedger.Id, LedgerEntryDirection.Debit, 100000m, Currency.NGN, 2);
        dbContext.LedgerEntries.AddRange(entry2Org, entry2Counter);

        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new ExportOrgWalletTransactionsQueryHandler(dbContext, orgContext);
        var result = await handler.Handle(new ExportOrgWalletTransactionsQuery(org.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("text/csv; charset=utf-8", result.ContentType);
        Assert.Equal("org-wallet-transactions.csv", result.FileName);

        var csv = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("Transaction ID,Reference,Date (UTC),Type,Direction,Amount (NGN),Status,Counterparty,Description", csv);
        Assert.Contains("REF-TXN-001", csv);
        Assert.Contains("25000.50", csv);
        Assert.Contains("Debit", csv);
        Assert.Contains("Vendor Disbursement", csv);
        Assert.Contains("REF-TXN-002", csv);
        Assert.Contains("100000.00", csv);
        Assert.Contains("Credit", csv);
        Assert.Contains("SystemSettlement", csv);
    }

    [Fact]
    public async Task Handle_WhenFilteredByTypeAndSearch_AppliesFiltersCorrectly()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var org = new Organization("Filter Org", "filter@org.com", "+2348000000000");
        dbContext.Organizations.Add(org);

        var wallet = Wallet.CreateOrganizationWallet(org.Id, Currency.NGN);
        dbContext.Wallets.Add(wallet);

        var orgLedger = LedgerAccount.CreateWalletAccount(wallet.Id, "Filter Org Wallet", Currency.NGN);
        dbContext.LedgerAccounts.Add(orgLedger);

        var settlement = LedgerAccount.CreateSystemAccount("Settlement", Currency.NGN, LedgerAccountType.SystemSettlement);
        dbContext.LedgerAccounts.Add(settlement);

        var tx1 = new LedgerTransaction(LedgerTransactionType.BankTransfer, "REF-OUT-001", null, "Supplier Payment");
        tx1.Complete(DateTime.UtcNow);
        dbContext.LedgerTransactions.Add(tx1);

        var entry1 = new LedgerEntry(tx1.Id, orgLedger.Id, LedgerEntryDirection.Debit, 5000m, Currency.NGN, 1);
        var entry1Sibling = new LedgerEntry(tx1.Id, settlement.Id, LedgerEntryDirection.Credit, 5000m, Currency.NGN, 2);
        dbContext.LedgerEntries.AddRange(entry1, entry1Sibling);

        var tx2 = new LedgerTransaction(LedgerTransactionType.VirtualAccountDeposit, "REF-IN-002", null, "Customer Deposit");
        tx2.Complete(DateTime.UtcNow);
        dbContext.LedgerTransactions.Add(tx2);

        var entry2 = new LedgerEntry(tx2.Id, orgLedger.Id, LedgerEntryDirection.Credit, 15000m, Currency.NGN, 1);
        var entry2Sibling = new LedgerEntry(tx2.Id, settlement.Id, LedgerEntryDirection.Debit, 15000m, Currency.NGN, 2);
        dbContext.LedgerEntries.AddRange(entry2, entry2Sibling);

        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new ExportOrgWalletTransactionsQueryHandler(dbContext, orgContext);

        // Filter by Debit direction
        var resultDebit = await handler.Handle(new ExportOrgWalletTransactionsQuery(org.Id, Type: "Debit"), CancellationToken.None);
        var csvDebit = Encoding.UTF8.GetString(resultDebit.Content);
        Assert.Contains("REF-OUT-001", csvDebit);
        Assert.DoesNotContain("REF-IN-002", csvDebit);

        // Search by "Customer"
        var resultSearch = await handler.Handle(new ExportOrgWalletTransactionsQuery(org.Id, Search: "Customer"), CancellationToken.None);
        var csvSearch = Encoding.UTF8.GetString(resultSearch.Content);
        Assert.Contains("REF-IN-002", csvSearch);
        Assert.DoesNotContain("REF-OUT-001", csvSearch);
    }

    [Fact]
    public async Task Handle_WhenUnauthorized_ThrowsUnauthorizedAccessException()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var orgId = Guid.NewGuid();

        orgContext.HasAccessToOrganizationAsync(orgId, Arg.Any<CancellationToken>()).Returns(false);

        var handler = new ExportOrgWalletTransactionsQueryHandler(dbContext, orgContext);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new ExportOrgWalletTransactionsQuery(orgId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenWalletDoesNotExist_ReturnsEmptyCsvWithHeader()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var orgId = Guid.NewGuid();

        orgContext.HasAccessToOrganizationAsync(orgId, Arg.Any<CancellationToken>()).Returns(true);

        var handler = new ExportOrgWalletTransactionsQueryHandler(dbContext, orgContext);
        var result = await handler.Handle(new ExportOrgWalletTransactionsQuery(orgId), CancellationToken.None);

        Assert.NotNull(result);
        var csv = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("Transaction ID,Reference,Date (UTC),Type,Direction,Amount (NGN),Status,Counterparty,Description", csv);
        Assert.DoesNotContain("REF-", csv);
    }
}
