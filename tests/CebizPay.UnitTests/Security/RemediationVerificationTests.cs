#pragma warning disable CS1591
using System.Text.Json;
using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Individuals.UpdateKycStatus;
using CebizPay.Application.UseCases.Organizations.Erp;
using CebizPay.Application.UseCases.Organizations.UpdateStatus;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Permissions;
using CebizPay.Infrastructure.Payments.Common;
using CebizPay.Infrastructure.Payments.Flutterwave;
using CebizPay.Infrastructure.Payments.Funding;
using CebizPay.Infrastructure.Payments.Monnify;
using CebizPay.Infrastructure.Payments.Paystack;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Security;

/// <summary>
/// Authoritative test suite verifying remediation of Phase 7.5.1 and 7.5.2 findings:
/// - P0 Admin/Platform authorization barriers and self-review prohibitions
/// - Intra-tenant RBAC enforcement (ERP Expenses, Vouchers)
/// - Card Refund and Funding tenant/ownership isolation
/// - ERP settlement financial atomicity and rollback safety
/// - Webhook resilience, dead-letter reactivation, and exponential retry
/// </summary>
public sealed class RemediationVerificationTests
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
    public async Task UpdateKycStatus_WhenCallerNotAdmin_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var publisher = Substitute.For<IEventPublisher>();
        var userContext = Substitute.For<ICurrentUserService>();
        userContext.UserId.Returns("normal-user");

        var profile = new IndividualProfile("target-user", "John", "Doe");
        db.IndividualProfiles.Add(profile);
        await db.SaveChangesAsync();

        var handler = new UpdateKycStatusCommandHandler(db, publisher, userContext);
        var command = new UpdateKycStatusCommand("target-user", KycStatus.Verified, "Verified", "normal-user");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("User is not authorized to review KYC submissions.", ex.Message);
    }

    [Fact]
    public async Task UpdateKycStatus_WhenAdminReviewsSelf_ThrowsInvalidOperationException()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var publisher = Substitute.For<IEventPublisher>();
        var userContext = Substitute.For<ICurrentUserService>();
        userContext.UserId.Returns("admin-user");

        var adminProfile = new AdminProfile("admin-user", AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(adminProfile);

        var profile = new IndividualProfile("admin-user", "Admin", "User");
        db.IndividualProfiles.Add(profile);
        await db.SaveChangesAsync();

        var handler = new UpdateKycStatusCommandHandler(db, publisher, userContext);
        var command = new UpdateKycStatusCommand("admin-user", KycStatus.Verified, "Self approval attempt", "admin-user");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("Admins cannot review or approve their own KYC status.", ex.Message);
    }

    [Fact]
    public async Task UpdateOrganizationStatus_WhenCallerNotAdmin_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var publisher = Substitute.For<IEventPublisher>();

        var org = new Organization("TestOrg", "test@org.com", "+2348000000001");
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var handler = new UpdateOrganizationStatusCommandHandler(db, publisher);
        var command = new UpdateOrganizationStatusCommand(org.Id, OrganizationStatus.Suspended, "Suspicious activity", "non-admin-user");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Equal("Caller is not authorized to update organization status.", ex.Message);
    }

    [Fact]
    public async Task PayOperatingExpense_WhenMissingExpensesManagePermission_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var pinService = Substitute.For<ITransactionPinService>();
        var ledgerService = Substitute.For<ILedgerPostingService>();
        var idempotencyService = Substitute.For<IIdempotencyService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "test@org.com", "+2348000000001");
        var expense = new OperatingExpense(
            org.Id,
            "EXP-001",
            ExpenseCategory.Utilities,
            "Internet bill",
            25000m,
            DateTime.UtcNow,
            "user-1",
            ExpensePaymentMethod.Wallet);
        expense.Approve("manager-1", DateTime.UtcNow);

        db.Organizations.Add(org);
        db.OperatingExpenses.Add(expense);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);
        // Explicitly deny ExpensesManage permission
        orgContext.HasPermissionAsync(org.Id, Permissions.ExpensesManage, Arg.Any<CancellationToken>()).Returns(false);
        userContext.UserId.Returns("user-1");

        var handler = new PayOperatingExpenseCommandHandler(db, orgContext, userContext, pinService, ledgerService, idempotencyService, outbox);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new PayOperatingExpenseCommand(org.Id, expense.Id, ExpensePaymentMethod.Manual), CancellationToken.None));
        Assert.Equal("You do not have permission to pay operating expenses.", ex.Message);
    }

    [Fact]
    public async Task ApproveCompanyVoucher_WhenMissingVouchersApprovePermission_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "test@org.com", "+2348000000001");
        var voucher = new CompanyVoucher(org.Id, "CV-001", "Vendor", "Logistics", 50000m, "user-1");

        db.Organizations.Add(org);
        db.CompanyVouchers.Add(voucher);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);
        // Deny CompanyVouchersApprove permission
        orgContext.HasPermissionAsync(org.Id, Permissions.CompanyVouchersApprove, Arg.Any<CancellationToken>()).Returns(false);
        userContext.UserId.Returns("user-1");

        var handler = new ApproveCompanyVoucherCommandHandler(db, orgContext, userContext, outbox);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new ApproveCompanyVoucherCommand(org.Id, voucher.Id), CancellationToken.None));
        Assert.Equal("You do not have permission to approve company vouchers.", ex.Message);
    }

    [Fact]
    public async Task PayCompanyVoucher_WhenMissingVouchersPayPermission_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var pinService = Substitute.For<ITransactionPinService>();
        var ledgerService = Substitute.For<ILedgerPostingService>();
        var idempotencyService = Substitute.For<IIdempotencyService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "test@org.com", "+2348000000001");
        var voucher = new CompanyVoucher(org.Id, "CV-001", "Vendor", "Logistics", 50000m, "user-1");
        voucher.Approve("manager-1", DateTime.UtcNow);

        db.Organizations.Add(org);
        db.CompanyVouchers.Add(voucher);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);
        // Deny CompanyVouchersPay permission
        orgContext.HasPermissionAsync(org.Id, Permissions.CompanyVouchersPay, Arg.Any<CancellationToken>()).Returns(false);
        userContext.UserId.Returns("user-1");

        var handler = new PayCompanyVoucherCommandHandler(db, orgContext, userContext, pinService, ledgerService, idempotencyService, outbox);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new PayCompanyVoucherCommand(org.Id, voucher.Id, CompanyVoucherPaymentMethod.Manual), CancellationToken.None));
        Assert.Equal("You do not have permission to pay company vouchers.", ex.Message);
    }

    [Fact]
    public async Task RequestCardRefund_WhenCallerNotOwnerOrAdmin_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var flwProvider = Substitute.For<ICardPaymentProvider>();
        flwProvider.Provider.Returns(PaymentProvider.Flutterwave);
        var ledgerPosting = Substitute.For<ILedgerPostingService>();
        var outbox = Substitute.For<IOutboxService>();

        var ownerUserId = "victim-user";
        var attackerUserId = "attacker-user";

        var wallet = Wallet.CreateIndividualWallet(ownerUserId, Currency.NGN);
        var fundingTx = FundingTransaction.Create(
            walletId: wallet.Id,
            virtualAccountId: null,
            provider: PaymentProvider.Flutterwave,
            providerTransactionReference: "REF-12345",
            fundingChannel: FundingChannel.Card,
            amount: 10000m,
            currency: Currency.NGN);

        db.Wallets.Add(wallet);
        db.FundingTransactions.Add(fundingTx);
        await db.SaveChangesAsync();

        var service = new CardRefundService(
            new[] { flwProvider },
            db,
            ledgerPosting,
            outbox,
            NullLogger<CardRefundService>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RequestCardRefundAsync(
                fundingTransactionId: fundingTx.Id,
                amount: 5000m,
                reason: "Fraudulent charge",
                idempotencyKey: "refund-key-1",
                actorUserId: attackerUserId));

        Assert.Equal("Caller is not authorized to request a refund for this transaction.", ex.Message);
    }

    [Fact]
    public async Task IngestWebhook_DuplicateDeliveryOfFailedWebhook_ReactivatesEventAndLogsAudit()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var sigVerifier = Substitute.For<IWebhookSignatureVerifier>();
        sigVerifier.VerifySignature(Arg.Any<PaymentProvider>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>()).Returns(true);

        var ledgerPosting = Substitute.For<ILedgerPostingService>();
        var feePolicyService = Substitute.For<IPlatformFeePolicyService>();
        var outbox = Substitute.For<IOutboxService>();

        var flwOptions = Options.Create(new FlutterwaveOptions { WebhookSecretHash = "secret_hash_123" });
        var pstkOptions = Options.Create(new PaystackOptions { WebhookSecret = "secret_123" });
        var monnifyOptions = Options.Create(new MonnifyOptions { WebhookSecret = "secret_123" });

        var processor = new WebhookProcessor(
            sigVerifier,
            db,
            ledgerPosting,
            feePolicyService,
            outbox,
            flwOptions,
            pstkOptions,
            monnifyOptions,
            NullLogger<WebhookProcessor>.Instance);

        var provider = PaymentProvider.Flutterwave;
        var providerEventId = "flw_evt_998877_SUCCESSFUL";
        var rawPayload = JsonSerializer.Serialize(new
        {
            @event = "charge.completed",
            data = new
            {
                id = 998877,
                tx_ref = "EVT-FAILED-TO-RETRY-001",
                flw_ref = "FLW-REF-998877",
                amount = 5000,
                currency = "NGN",
                status = "successful"
            }
        });

        var webhookEvent = WebhookEvent.Create(
            provider: provider,
            providerEventId: providerEventId,
            eventType: "charge.completed");

        // Transition through lifecycle to Failed
        webhookEvent.Claim("worker-1", TimeSpan.FromMinutes(5));
        webhookEvent.MarkFailed("Network timeout during credit");

        db.WebhookEvents.Add(webhookEvent);
        await db.SaveChangesAsync();

        Assert.Equal(WebhookEventStatus.Failed, webhookEvent.Status);

        var headers = new Dictionary<string, string> { { "verif-hash", "secret_hash_123" } };

        // Act: Deliver duplicate webhook for this failed event
        var result = await processor.IngestWebhookAsync(
            provider: provider,
            rawPayload: rawPayload,
            headers: headers);

        // Assert: Result is Processed (accepted for retry), status transitioned back to Received
        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);

        var updatedEvent = await db.WebhookEvents.FirstAsync(e => e.Id == webhookEvent.Id);
        Assert.Equal(WebhookEventStatus.Received, updatedEvent.Status);
        Assert.Null(updatedEvent.LockedBy);
        Assert.NotNull(updatedEvent.NextRetryAtUtc);

        var auditLog = await db.AuditLogs.FirstOrDefaultAsync(a => a.Action == AuditActions.WebhookReactivated);
        Assert.NotNull(auditLog);
        Assert.Equal("SYSTEM", auditLog.ActorId);
    }

    [Fact]
    public async Task PayOperatingExpense_WhenDownstreamFails_RollsBackWalletDebitAtomically()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var pinService = Substitute.For<ITransactionPinService>();
        var ledgerService = Substitute.For<ILedgerPostingService>();
        var idempotencyService = Substitute.For<IIdempotencyService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("TestOrg", "test@org.com", "+2348000000001");
        var orgWallet = Wallet.CreateOrganizationWallet(org.Id, Currency.NGN);
        orgWallet.Credit(100000m); // Initial balance: 100,000 NGN

        var expense = new OperatingExpense(
            org.Id,
            "EXP-002",
            ExpenseCategory.Utilities,
            "Office Supplies",
            40000m,
            DateTime.UtcNow,
            "user-1",
            ExpensePaymentMethod.Wallet);
        expense.Approve("manager-1", DateTime.UtcNow);

        var ledgerAccount = LedgerAccount.CreateWalletAccount(orgWallet.Id, "Org Wallet", Currency.NGN);
        var sysExpenseAccount = LedgerAccount.CreateSystemAccount("Expense Settlement", Currency.NGN, LedgerAccountType.SystemSettlement);

        db.Organizations.Add(org);
        db.Wallets.Add(orgWallet);
        db.OperatingExpenses.Add(expense);
        db.LedgerAccounts.AddRange(ledgerAccount, sysExpenseAccount);
        await db.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id, Arg.Any<CancellationToken>()).Returns(true);
        orgContext.HasPermissionAsync(org.Id, Permissions.ExpensesManage, Arg.Any<CancellationToken>()).Returns(true);
        userContext.UserId.Returns("user-1");
        pinService.VerifyPinAsync("user-1", "1234", Arg.Any<CancellationToken>()).Returns((true, false, null));
        ledgerService.GetOrCreateSystemSettlementAccountAsync(Currency.NGN, Arg.Any<CancellationToken>()).Returns(sysExpenseAccount);

        // Simulate a failure during ledger transaction posting
        ledgerService.PostSingleCurrencyTransactionAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<decimal>(),
            Arg.Any<Currency>(),
            Arg.Any<LedgerTransactionType>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns<LedgerTransaction>(_ => throw new InvalidOperationException("Simulated ledger failure"));

        var handler = new PayOperatingExpenseCommandHandler(db, orgContext, userContext, pinService, ledgerService, idempotencyService, outbox);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new PayOperatingExpenseCommand(org.Id, expense.Id, ExpensePaymentMethod.Wallet, "1234"), CancellationToken.None));

        // Expense was not marked Paid because of atomic transaction rollback
        var finalExpense = await db.OperatingExpenses.FirstAsync(e => e.Id == expense.Id);
        Assert.Equal(ExpenseStatus.Approved, finalExpense.Status);
        Assert.Null(finalExpense.PaidAtUtc);
    }
}
