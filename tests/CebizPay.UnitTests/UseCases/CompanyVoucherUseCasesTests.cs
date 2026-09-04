#pragma warning disable CS1591
using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Organizations.Erp;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.UseCases;

/// <summary>
/// Application use cases unit tests for Phase 5E CompanyVoucher features.
/// </summary>
public sealed class CompanyVoucherUseCasesTests
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
    public async Task CreateCompanyVoucher_ValidRequest_CreatesVoucherAndOutboxEvent()
    {
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);
        userContext.UserId.Returns("user-admin");

        var handler = new CreateCompanyVoucherCommandHandler(db, orgContext, userContext, outbox);
        var command = new CreateCompanyVoucherCommand(
            org.Id,
            "Office Supply Depot",
            "Purchase of computer monitors",
            180000m,
            Currency.NGN,
            CompanyVoucherPaymentMethod.Wallet,
            "Bank: GTBank, Acc: 0112233445");

        var voucherId = await handler.Handle(command, CancellationToken.None);

        var voucher = await db.CompanyVouchers.FirstOrDefaultAsync(v => v.Id == voucherId);
        Assert.NotNull(voucher);
        Assert.Equal(CompanyVoucherStatus.Draft, voucher.Status);
        Assert.Equal(180000m, voucher.Amount);
        Assert.Equal("Office Supply Depot", voucher.PayeeName);
        Assert.StartsWith("CV-", voucher.VoucherNumber);
    }

    [Fact]
    public async Task ApproveCompanyVoucher_DraftVoucher_TransitionsToApproved()
    {
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        var voucher = new CompanyVoucher(org.Id, "CV-001", "Vendor A", "Services", 50000m, "user-admin");

        db.Organizations.Add(org);
        db.CompanyVouchers.Add(voucher);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);
        orgContext.HasPermissionAsync(org.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        userContext.UserId.Returns("manager-1");

        var handler = new ApproveCompanyVoucherCommandHandler(db, orgContext, userContext, outbox);
        await handler.Handle(new ApproveCompanyVoucherCommand(org.Id, voucher.Id), CancellationToken.None);

        var updatedVoucher = await db.CompanyVouchers.FirstAsync(v => v.Id == voucher.Id);
        Assert.Equal(CompanyVoucherStatus.Approved, updatedVoucher.Status);
        Assert.Equal("manager-1", updatedVoucher.ApprovedByUserId);
        Assert.NotNull(updatedVoucher.ApprovedAtUtc);
    }

    [Fact]
    public async Task PayCompanyVoucher_WalletSettlement_DebitsOrgWalletAndPostsLedger()
    {
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var pinService = Substitute.For<ITransactionPinService>();
        var ledgerService = Substitute.For<ILedgerPostingService>();
        var idempotencyService = Substitute.For<IIdempotencyService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        var orgWallet = Wallet.CreateOrganizationWallet(org.Id, Currency.NGN);
        orgWallet.Credit(300000m); // 300,000 NGN balance

        var ledgerAccount = LedgerAccount.CreateWalletAccount(orgWallet.Id, "Org Wallet", Currency.NGN);
        var sysDisbursementAccount = LedgerAccount.CreateSystemAccount("Disbursement Settlement", Currency.NGN, LedgerAccountType.SystemSettlement);

        var voucher = new CompanyVoucher(
            org.Id,
            "CV-001",
            "Contractor Team",
            "Electrical wiring maintenance",
            75000m,
            "user-1",
            Currency.NGN,
            CompanyVoucherPaymentMethod.Wallet);
        voucher.Approve("manager-1", DateTime.UtcNow);

        db.Organizations.Add(org);
        db.Wallets.Add(orgWallet);
        db.LedgerAccounts.AddRange(ledgerAccount, sysDisbursementAccount);
        db.CompanyVouchers.Add(voucher);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);
        orgContext.HasPermissionAsync(org.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        userContext.UserId.Returns("user-1");
        pinService.VerifyPinAsync("user-1", "1234", Arg.Any<CancellationToken>()).Returns((true, false, null));
        ledgerService.GetOrCreateSystemSettlementAccountAsync(Currency.NGN, Arg.Any<CancellationToken>()).Returns(sysDisbursementAccount);
        ledgerService.PostSingleCurrencyTransactionAsync(
            ledgerAccount.Id,
            sysDisbursementAccount.Id,
            75000m,
            Currency.NGN,
            LedgerTransactionType.CompanyVoucherDisbursement,
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(new LedgerTransaction(LedgerTransactionType.CompanyVoucherDisbursement, "CV-001", null, "Disbursement"));

        idempotencyService.CreateRecordAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>())
            .Returns(new IdempotencyRecord("key-1", "PayCompanyVoucher", "hash", "user-1", org.Id));

        var handler = new PayCompanyVoucherCommandHandler(db, orgContext, userContext, pinService, ledgerService, idempotencyService, outbox);
        await handler.Handle(new PayCompanyVoucherCommand(org.Id, voucher.Id, CompanyVoucherPaymentMethod.Wallet, "1234", "key-1"), CancellationToken.None);

        var paidVoucher = await db.CompanyVouchers.FirstAsync(v => v.Id == voucher.Id);
        Assert.Equal(CompanyVoucherStatus.Paid, paidVoucher.Status);
        Assert.NotNull(paidVoucher.PaidAtUtc);
        Assert.NotNull(paidVoucher.WalletId);
        Assert.NotNull(paidVoucher.LedgerTransactionId);
    }

    [Fact]
    public async Task PayCompanyVoucher_ManualSettlement_MarksPaidWithoutLedgerPost()
    {
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var pinService = Substitute.For<ITransactionPinService>();
        var ledgerService = Substitute.For<ILedgerPostingService>();
        var idempotencyService = Substitute.For<IIdempotencyService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        var voucher = new CompanyVoucher(
            org.Id,
            "CV-002",
            "Landlord",
            "Lease payment",
            500000m,
            "user-1",
            Currency.NGN,
            CompanyVoucherPaymentMethod.Manual);
        voucher.Approve("manager-1", DateTime.UtcNow);

        db.Organizations.Add(org);
        db.CompanyVouchers.Add(voucher);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);
        orgContext.HasPermissionAsync(org.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        userContext.UserId.Returns("user-1");

        var handler = new PayCompanyVoucherCommandHandler(db, orgContext, userContext, pinService, ledgerService, idempotencyService, outbox);
        await handler.Handle(new PayCompanyVoucherCommand(org.Id, voucher.Id, CompanyVoucherPaymentMethod.Manual, Reference: "CHQ-100293"), CancellationToken.None);

        var paidVoucher = await db.CompanyVouchers.FirstAsync(v => v.Id == voucher.Id);
        Assert.Equal(CompanyVoucherStatus.Paid, paidVoucher.Status);
        Assert.Equal("CHQ-100293", paidVoucher.Reference);
        Assert.Null(paidVoucher.LedgerTransactionId);
    }

    [Fact]
    public async Task CompanyVoucher_WhenOrgIsSuspended_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "info@test.com", "+2348000000001");
        org.TransitionStatus(OrganizationStatus.Verified);
        org.TransitionStatus(OrganizationStatus.Suspended);

        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);
        userContext.UserId.Returns("user-admin");

        var handler = new CreateCompanyVoucherCommandHandler(db, orgContext, userContext, outbox);
        var command = new CreateCompanyVoucherCommand(
            org.Id,
            "Payee",
            "Purpose",
            10000m,
            Currency.NGN);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
    }
}
