using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Interfaces.Support;
using CebizPay.Application.Common.Support;
using CebizPay.Application.UseCases.Support;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Support.Entities;
using CebizPay.Domain.Support.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Support;

public class SupportApplicationUseCasesTests
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
    public async Task CreateTicket_DirectSubmission_CreatesTicketAndInitialMessage()
    {
        await using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("user_cust_01");

        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        orgContext.CurrentOrganizationId.Returns((Guid?)null);

        var generator = new SupportTicketNumberGenerator();
        var audit = Substitute.For<IAuditLogService>();
        var outbox = Substitute.For<IOutboxService>();

        var handler = new CreateSupportTicketCommandHandler(db, currentUserService, orgContext, generator, audit, outbox);

        var result = await handler.Handle(new CreateSupportTicketCommand(
            Category: SupportTicketCategory.PaymentOrTransfer,
            Subject: "Failed Transfer to Access Bank",
            Description: "I tried sending 5,000 NGN and it failed.",
            Priority: SupportTicketPriority.High), CancellationToken.None);

        Assert.NotNull(result.TicketNumber);
        Assert.Equal("user_cust_01", result.UserId);
        Assert.Equal(SupportTicketStatus.Open, result.Status);
        Assert.Equal(SupportTicketPriority.High, result.Priority);
        Assert.Single(result.Messages);
        Assert.Equal("I tried sending 5,000 NGN and it failed.", result.Messages[0].Content);

        var persisted = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == result.Id);
        Assert.NotNull(persisted);
        Assert.Equal(result.TicketNumber, persisted.TicketNumber);
    }

    [Fact]
    public async Task CreateTicket_OfflineIdempotencyKey_DeduplicatesAndReturnsExistingTicket()
    {
        await using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("user_offline_01");

        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var generator = new SupportTicketNumberGenerator();
        var audit = Substitute.For<IAuditLogService>();

        var handler = new CreateSupportTicketCommandHandler(db, currentUserService, orgContext, generator, audit);

        var idempotencyKey = "CLIENT-OFFLINE-SYNC-KEY-999";

        // First call: creates ticket
        var result1 = await handler.Handle(new CreateSupportTicketCommand(
            Category: SupportTicketCategory.WalletOrAccount,
            Subject: "Offline Issue",
            Description: "Submitted while offline",
            IdempotencyKey: idempotencyKey), CancellationToken.None);

        // Duplicate replay call: returns existing ticket without duplicating
        var result2 = await handler.Handle(new CreateSupportTicketCommand(
            Category: SupportTicketCategory.WalletOrAccount,
            Subject: "Offline Issue",
            Description: "Submitted while offline",
            IdempotencyKey: idempotencyKey), CancellationToken.None);

        Assert.Equal(result1.Id, result2.Id);
        Assert.Equal(result1.TicketNumber, result2.TicketNumber);

        var totalTickets = await db.SupportTickets.CountAsync();
        Assert.Equal(1, totalTickets);
    }

    [Fact]
    public async Task GetSupportTickets_TenantAndUserIsolation_OnlyReturnsOwnTickets()
    {
        await using var db = CreateInMemoryDbContext();
        var userA = "user_alpha";
        var userB = "user_beta";

        var ticketA = SupportTicket.Create("NUM-A", userA, null, SupportTicketCategory.Other, "Alpha ticket", "Desc", SupportTicketPriority.Normal, DateTime.UtcNow);
        var ticketB = SupportTicket.Create("NUM-B", userB, null, SupportTicketCategory.Other, "Beta ticket", "Desc", SupportTicketPriority.Normal, DateTime.UtcNow);
        db.SupportTickets.AddRange(ticketA, ticketB);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(userA);

        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        orgContext.CurrentOrganizationId.Returns((Guid?)null);

        var handler = new GetSupportTicketsQueryHandler(db, currentUserService, orgContext);
        var paged = await handler.Handle(new GetSupportTicketsQuery(), CancellationToken.None);

        Assert.Equal(1, paged.TotalCount);
        Assert.Equal("NUM-A", paged.Items[0].TicketNumber);
    }

    [Fact]
    public async Task GetTicketById_NonOwner_ThrowsKeyNotFoundException_ForIdorProtection()
    {
        await using var db = CreateInMemoryDbContext();
        var owner = "user_owner";
        var attacker = "user_attacker";

        var ticket = SupportTicket.Create("NUM-SECRET", owner, null, SupportTicketCategory.PaymentOrTransfer, "Secret dispute", "Private message", SupportTicketPriority.High, DateTime.UtcNow);
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(attacker);

        var handler = new GetSupportTicketByIdQueryHandler(db, currentUserService);

        // Disguised as not found to prevent IDOR existence disclosure
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new GetSupportTicketByIdQuery(ticket.Id), CancellationToken.None));
    }

    [Fact]
    public async Task AddMessage_CustomerAppendsMessageToThread()
    {
        await using var db = CreateInMemoryDbContext();
        var user = "user_thread";

        var ticket = SupportTicket.Create("NUM-THREAD", user, null, SupportTicketCategory.Other, "General", "Initial", SupportTicketPriority.Normal, DateTime.UtcNow);
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(user);

        var handler = new AddTicketMessageCommandHandler(db, currentUserService);
        var msgResult = await handler.Handle(new AddTicketMessageCommand(ticket.Id, "Customer follow-up note."), CancellationToken.None);

        Assert.Equal("Customer follow-up note.", msgResult.Content);
        Assert.Equal(TicketMessageSenderType.Customer, msgResult.SenderType);

        var count = await db.TicketMessages.CountAsync(m => m.TicketId == ticket.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CloseTicket_CustomerClosesOwnTicket()
    {
        await using var db = CreateInMemoryDbContext();
        var user = "user_closer";

        var ticket = SupportTicket.Create("NUM-CLOSE", user, null, SupportTicketCategory.Other, "Close me", "Initial", SupportTicketPriority.Normal, DateTime.UtcNow);
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(user);

        var audit = Substitute.For<IAuditLogService>();
        var handler = new CloseSupportTicketCommandHandler(db, currentUserService, audit);

        var success = await handler.Handle(new CloseSupportTicketCommand(ticket.Id), CancellationToken.None);
        Assert.True(success);

        var updated = await db.SupportTickets.FirstAsync(t => t.Id == ticket.Id);
        Assert.Equal(SupportTicketStatus.Closed, updated.Status);
        Assert.NotNull(updated.ClosedAtUtc);
    }

    [Fact]
    public async Task AdminUpdateStatus_SuperAdminResolvesTicket_RequiresSummary()
    {
        await using var db = CreateInMemoryDbContext();
        var admin = new AdminProfile("super_admin_user", AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(admin);

        var ticket = SupportTicket.Create("NUM-RESOLVE", "cust_01", null, SupportTicketCategory.PaymentOrTransfer, "Issue", "Desc", SupportTicketPriority.Normal, DateTime.UtcNow);
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("super_admin_user");

        var audit = Substitute.For<IAuditLogService>();
        var outbox = Substitute.For<IOutboxService>();
        var handler = new AdminUpdateTicketStatusCommandHandler(db, currentUserService, audit, outbox);

        var result = await handler.Handle(new AdminUpdateTicketStatusCommand(
            TicketId: ticket.Id,
            Status: SupportTicketStatus.Resolved,
            ResolutionSummary: "Interbank transaction traced and confirmed settled with destination bank."), CancellationToken.None);

        Assert.Equal(SupportTicketStatus.Resolved, result.Status);
        Assert.Equal("Interbank transaction traced and confirmed settled with destination bank.", result.ResolutionSummary);
        Assert.NotNull(result.ResolvedAtUtc);
    }

    [Fact]
    public async Task AdminUpdateStatus_NonSuperAdmin_ThrowsUnauthorizedAccessException()
    {
        await using var db = CreateInMemoryDbContext();
        var admin = new AdminProfile("auditor_user", AdminRoleType.Auditor);
        db.AdminProfiles.Add(admin);

        var ticket = SupportTicket.Create("NUM-AUTH", "cust_01", null, SupportTicketCategory.Other, "Issue", "Desc", SupportTicketPriority.Normal, DateTime.UtcNow);
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("auditor_user");

        var handler = new AdminUpdateTicketStatusCommandHandler(db, currentUserService, Substitute.For<IAuditLogService>());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new AdminUpdateTicketStatusCommand(ticket.Id, SupportTicketStatus.Resolved, "Resolution"), CancellationToken.None));
    }

    [Fact]
    public async Task AdminAddMessage_SuperAdmin_SetsFirstResponseAtUtc()
    {
        await using var db = CreateInMemoryDbContext();
        var admin = new AdminProfile("super_admin_msg", AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(admin);

        var ticket = SupportTicket.Create("NUM-RESP", "cust_01", null, SupportTicketCategory.Other, "Issue", "Desc", SupportTicketPriority.Normal, DateTime.UtcNow);
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("super_admin_msg");

        var handler = new AdminAddTicketMessageCommandHandler(db, currentUserService);
        var msg = await handler.Handle(new AdminAddTicketMessageCommand(ticket.Id, "Hello from operator."), CancellationToken.None);

        Assert.Equal(TicketMessageSenderType.Admin, msg.SenderType);

        var updated = await db.SupportTickets.FirstAsync(t => t.Id == ticket.Id);
        Assert.NotNull(updated.FirstResponseAtUtc);
    }

    [Fact]
    public async Task GetSupportReports_SuperAdmin_AggregatesCountsAccurately()
    {
        await using var db = CreateInMemoryDbContext();
        var admin = new AdminProfile("super_reporter", AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(admin);

        var now = DateTime.UtcNow;
        var t1 = SupportTicket.Create("REP-1", "u1", null, SupportTicketCategory.PaymentOrTransfer, "Sub", "Desc", SupportTicketPriority.Critical, now);
        var t2 = SupportTicket.Create("REP-2", "u2", null, SupportTicketCategory.WalletOrAccount, "Sub", "Desc", SupportTicketPriority.Normal, now);
        t2.Resolve("Resolved", now);
        db.SupportTickets.AddRange(t1, t2);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("super_reporter");

        var handler = new GetSupportReportsQueryHandler(db, currentUserService);
        var reports = await handler.Handle(new GetSupportReportsQuery(), CancellationToken.None);

        Assert.Equal(2, reports.TotalTickets);
        Assert.Equal(1, reports.OpenTickets);
        Assert.Equal(1, reports.ResolvedTickets);
        Assert.Equal(1, reports.TicketsByCategory[SupportTicketCategory.PaymentOrTransfer.ToString()]);
        Assert.Equal(1, reports.TicketsByPriority[SupportTicketPriority.Critical.ToString()]);
    }

    [Fact]
    public async Task CheckSupportSla_DetectsBreachedTickets_IdempotentlyMarksBreached()
    {
        await using var db = CreateInMemoryDbContext();
        var now = DateTime.UtcNow;

        // Ticket created 13 hours ago (SLA deadline was 1 hour ago)
        var breachedTicket = SupportTicket.Create("SLA-BREACH", "u1", null, SupportTicketCategory.PaymentOrTransfer, "Late issue", "Desc", SupportTicketPriority.High, now.AddHours(-13));

        // Ticket created 1 hour ago (SLA deadline in 11 hours)
        var activeTicket = SupportTicket.Create("SLA-OK", "u2", null, SupportTicketCategory.PaymentOrTransfer, "Recent issue", "Desc", SupportTicketPriority.Normal, now.AddHours(-1));

        db.SupportTickets.AddRange(breachedTicket, activeTicket);
        await db.SaveChangesAsync();

        var audit = Substitute.For<IAuditLogService>();
        var outbox = Substitute.For<IOutboxService>();
        var handler = new CheckSupportSlaCommandHandler(db, audit, NullLogger<CheckSupportSlaCommandHandler>.Instance, outbox);

        // First run: detects 1 breach
        var count1 = await handler.Handle(new CheckSupportSlaCommand(100), CancellationToken.None);
        Assert.Equal(1, count1);

        var updatedBreached = await db.SupportTickets.FirstAsync(t => t.Id == breachedTicket.Id);
        Assert.True(updatedBreached.IsSlaBreached);
        Assert.NotNull(updatedBreached.SlaBreachedAtUtc);

        var updatedActive = await db.SupportTickets.FirstAsync(t => t.Id == activeTicket.Id);
        Assert.False(updatedActive.IsSlaBreached);

        // Second run: idempotent, returns 0 because already marked
        var count2 = await handler.Handle(new CheckSupportSlaCommand(100), CancellationToken.None);
        Assert.Equal(0, count2);
    }

    [Fact]
    public async Task FinancialSafety_SupportSystem_CausesZeroLedgerEntriesAndZeroWalletMutations()
    {
        await using var db = CreateInMemoryDbContext();
        var user = "user_financial_safety";

        var wallet = Wallet.CreateIndividualWallet(user, Currency.NGN);
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        var initialWalletBalance = wallet.AvailableBalance;

        // Perform Kola session, ticket creation, message posting, and administrative resolution
        var generator = new SupportTicketNumberGenerator();
        var audit = Substitute.For<IAuditLogService>();
        var outbox = Substitute.For<IOutboxService>();
        var kolaService = new KolaChatbotService(db, generator, audit, outbox);

        var kolaResp = await kolaService.ProcessInputAsync(new KolaSessionInput(
            SessionId: "fin_safety_session",
            UserId: user,
            OrganizationId: null,
            CurrentState: KolaSessionState.CategorySelected,
            Category: SupportTicketCategory.PaymentOrTransfer,
            SelectedIssueIndex: null,
            UserMessage: "Money was deducted from my account without authorization"));

        Assert.NotNull(kolaResp.CreatedTicketId);

        // Add message
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(user);
        var addMsgHandler = new AddTicketMessageCommandHandler(db, currentUserService);
        await addMsgHandler.Handle(new AddTicketMessageCommand(kolaResp.CreatedTicketId.Value, "Please refund my money!"), CancellationToken.None);

        // SuperAdmin resolves ticket
        var admin = new AdminProfile("super_admin_fin", AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(admin);
        await db.SaveChangesAsync();

        currentUserService.UserId.Returns("super_admin_fin");
        var resolveHandler = new AdminUpdateTicketStatusCommandHandler(db, currentUserService, audit, outbox);
        await resolveHandler.Handle(new AdminUpdateTicketStatusCommand(
            kolaResp.CreatedTicketId.Value,
            SupportTicketStatus.Resolved,
            "Explained that money movement requires official bank dispute resolution."), CancellationToken.None);

        // Assert 1: Wallet balance remains unchanged
        var reloadedWallet = await db.Wallets.FirstAsync(w => w.Id == wallet.Id);
        Assert.Equal(initialWalletBalance, reloadedWallet.AvailableBalance);
        Assert.Equal(0m, reloadedWallet.AvailableBalance);

        // Assert 2: Double-entry ledger transactions count is exactly 0
        var ledgerTxCount = await db.LedgerTransactions.CountAsync();
        Assert.Equal(0, ledgerTxCount);

        // Assert 3: Ledger entries count is exactly 0
        var ledgerEntryCount = await db.LedgerEntries.CountAsync();
        Assert.Equal(0, ledgerEntryCount);
    }
}
