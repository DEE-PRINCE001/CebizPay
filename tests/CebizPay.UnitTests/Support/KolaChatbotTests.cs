using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Interfaces.Support;
using CebizPay.Application.Common.Support;
using CebizPay.Domain.Support.Entities;
using CebizPay.Domain.Support.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Support;

public class KolaChatbotTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static (KolaChatbotService service, ApplicationDbContext db) CreateService()
    {
        var db = CreateInMemoryDbContext();
        var generator = new SupportTicketNumberGenerator();
        var audit = Substitute.For<IAuditLogService>();
        var outbox = Substitute.For<IOutboxService>();

        var service = new KolaChatbotService(db, generator, audit, outbox);
        return (service, db);
    }

    [Fact]
    public void StartSession_ReturnsGreetingAnd6RootCategories()
    {
        var (service, _) = CreateService();

        var response = service.StartSession("user_01");

        Assert.NotNull(response.SessionId);
        Assert.Equal(KolaSessionState.Started, response.State);
        Assert.Null(response.Category);
        Assert.Equal(6, response.Options.Count);
        Assert.Contains("Payment / Transfer", response.Options[0]);
        Assert.Contains("Wallet / Account", response.Options[1]);
        Assert.Contains("KYC / Verification", response.Options[2]);
        Assert.Contains("Savings / Thrift", response.Options[3]);
        Assert.Contains("Business / Workplace", response.Options[4]);
        Assert.Contains("Something Else", response.Options[5]);
        Assert.False(response.IsEscalated);
    }

    [Theory]
    [InlineData("1", SupportTicketCategory.PaymentOrTransfer, 4)]
    [InlineData("2", SupportTicketCategory.WalletOrAccount, 4)]
    [InlineData("3", SupportTicketCategory.KycOrVerification, 3)]
    [InlineData("4", SupportTicketCategory.SavingsOrThrift, 4)]
    [InlineData("5", SupportTicketCategory.BusinessOrWorkplace, 4)]
    [InlineData("6", SupportTicketCategory.Other, 2)]
    public async Task CategorySelection_All6Categories_ReturnRespectiveSubIssues(
        string inputNumber,
        SupportTicketCategory expectedCategory,
        int expectedSubIssueCount)
    {
        var (service, _) = CreateService();
        var session = service.StartSession("user_02");

        var response = await service.ProcessInputAsync(new KolaSessionInput(
            SessionId: session.SessionId,
            UserId: "user_02",
            OrganizationId: null,
            CurrentState: KolaSessionState.Started,
            Category: null,
            SelectedIssueIndex: null,
            UserMessage: inputNumber));

        Assert.Equal(KolaSessionState.CategorySelected, response.State);
        Assert.Equal(expectedCategory, response.Category);
        Assert.Equal(expectedSubIssueCount, response.Options.Count);
        Assert.False(response.IsEscalated);
    }

    [Fact]
    public async Task CategorySelection_InvalidInput_PromptsUserToSelect1To6()
    {
        var (service, _) = CreateService();
        var session = service.StartSession("user_03");

        var response = await service.ProcessInputAsync(new KolaSessionInput(
            SessionId: session.SessionId,
            UserId: "user_03",
            OrganizationId: null,
            CurrentState: KolaSessionState.Started,
            Category: null,
            SelectedIssueIndex: null,
            UserMessage: "99"));

        Assert.Equal(KolaSessionState.Started, response.State);
        Assert.Contains("select one of the numbered options below (1 to 6)", response.BotMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(6, response.Options.Count);
    }

    [Theory]
    [InlineData("restart")]
    [InlineData("reset")]
    [InlineData("menu")]
    [InlineData("start over")]
    public async Task RestartCommand_ResetsToRootCategories(string resetCommand)
    {
        var (service, _) = CreateService();
        var session = service.StartSession("user_04");

        var response = await service.ProcessInputAsync(new KolaSessionInput(
            SessionId: session.SessionId,
            UserId: "user_04",
            OrganizationId: null,
            CurrentState: KolaSessionState.CategorySelected,
            Category: SupportTicketCategory.PaymentOrTransfer,
            SelectedIssueIndex: 1,
            UserMessage: resetCommand));

        Assert.Equal(KolaSessionState.Started, response.State);
        Assert.Null(response.Category);
        Assert.Equal(6, response.Options.Count);
    }

    [Theory]
    [InlineData("I want to speak with a human agent")]
    [InlineData("Can I talk to a human?")]
    [InlineData("representative please")]
    [InlineData("speak to someone")]
    public async Task HumanEscalationKeywords_ImmediatelyEscalatesAndCreatesTicket(string message)
    {
        var (service, db) = CreateService();
        var session = service.StartSession("user_05");

        var response = await service.ProcessInputAsync(new KolaSessionInput(
            SessionId: session.SessionId,
            UserId: "user_05",
            OrganizationId: null,
            CurrentState: KolaSessionState.CategorySelected,
            Category: SupportTicketCategory.PaymentOrTransfer,
            SelectedIssueIndex: null,
            UserMessage: message));

        Assert.Equal(KolaSessionState.TicketCreated, response.State);
        Assert.True(response.IsEscalated);
        Assert.Equal(SupportTicketPriority.High, response.Priority);
        Assert.NotNull(response.CreatedTicketId);
        Assert.NotNull(response.CreatedTicketNumber);

        // Verify ticket in DB
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == response.CreatedTicketId);
        Assert.NotNull(ticket);
        Assert.Equal(SupportTicketStatus.Escalated, ticket.Status);
        Assert.Equal(SupportTicketPriority.High, ticket.Priority);
    }

    [Fact]
    public async Task CriticalFinancialIssue_UnrecognizedTransaction_EscalatesWithCriticalPriority()
    {
        var (service, db) = CreateService();
        var session = service.StartSession("user_06");

        // Category 1: Payment -> Issue 4: "I don't recognize this transaction"
        var response = await service.ProcessInputAsync(new KolaSessionInput(
            SessionId: session.SessionId,
            UserId: "user_06",
            OrganizationId: null,
            CurrentState: KolaSessionState.CategorySelected,
            Category: SupportTicketCategory.PaymentOrTransfer,
            SelectedIssueIndex: null,
            UserMessage: "4"));

        Assert.Equal(KolaSessionState.TicketCreated, response.State);
        Assert.True(response.IsEscalated);
        Assert.Equal(SupportTicketPriority.Critical, response.Priority);
        Assert.Contains("CRITICAL", response.BotMessage, StringComparison.OrdinalIgnoreCase);

        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == response.CreatedTicketId);
        Assert.NotNull(ticket);
        Assert.Equal(SupportTicketPriority.Critical, ticket.Priority);
    }

    [Fact]
    public async Task CriticalFinancialIssue_WalletBalanceLooksWrong_EscalatesWithCriticalPriority()
    {
        var (service, db) = CreateService();
        var session = service.StartSession("user_07");

        // Category 2: Wallet -> Issue 2: "Wallet balance looks wrong"
        var response = await service.ProcessInputAsync(new KolaSessionInput(
            SessionId: session.SessionId,
            UserId: "user_07",
            OrganizationId: null,
            CurrentState: KolaSessionState.CategorySelected,
            Category: SupportTicketCategory.WalletOrAccount,
            SelectedIssueIndex: null,
            UserMessage: "2"));

        Assert.Equal(KolaSessionState.TicketCreated, response.State);
        Assert.True(response.IsEscalated);
        Assert.Equal(SupportTicketPriority.Critical, response.Priority);

        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == response.CreatedTicketId);
        Assert.NotNull(ticket);
        Assert.Equal(SupportTicketPriority.Critical, ticket.Priority);
    }

    [Fact]
    public async Task SelfServiceResolution_Option1_TransitionsToResolvedWithoutTicket()
    {
        var (service, db) = CreateService();

        var response = await service.ProcessInputAsync(new KolaSessionInput(
            SessionId: "session_08",
            UserId: "user_08",
            OrganizationId: null,
            CurrentState: KolaSessionState.ResolutionSuggested,
            Category: SupportTicketCategory.PaymentOrTransfer,
            SelectedIssueIndex: 0,
            UserMessage: "1"));

        Assert.Equal(KolaSessionState.Resolved, response.State);
        Assert.Contains("glad we could help", response.BotMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Null(response.CreatedTicketId);

        var ticketCount = await db.SupportTickets.CountAsync();
        Assert.Equal(0, ticketCount);
    }

    [Fact]
    public async Task SelfServiceResolution_Option2_CreatesTicketWithNormalPriority()
    {
        var (service, db) = CreateService();

        var response = await service.ProcessInputAsync(new KolaSessionInput(
            SessionId: "session_09",
            UserId: "user_09",
            OrganizationId: null,
            CurrentState: KolaSessionState.ResolutionSuggested,
            Category: SupportTicketCategory.PaymentOrTransfer,
            SelectedIssueIndex: 0,
            UserMessage: "2"));

        Assert.Equal(KolaSessionState.TicketCreated, response.State);
        Assert.True(response.IsEscalated);
        Assert.Equal(SupportTicketPriority.Normal, response.Priority);
        Assert.NotNull(response.CreatedTicketId);

        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == response.CreatedTicketId);
        Assert.NotNull(ticket);
        Assert.Equal(SupportTicketPriority.Normal, ticket.Priority);
    }

    [Fact]
    public async Task SelfServiceResolution_Option3_CreatesTicketWithHighPriority()
    {
        var (service, db) = CreateService();

        var response = await service.ProcessInputAsync(new KolaSessionInput(
            SessionId: "session_10",
            UserId: "user_10",
            OrganizationId: null,
            CurrentState: KolaSessionState.ResolutionSuggested,
            Category: SupportTicketCategory.KycOrVerification,
            SelectedIssueIndex: 0,
            UserMessage: "3"));

        Assert.Equal(KolaSessionState.TicketCreated, response.State);
        Assert.True(response.IsEscalated);
        Assert.Equal(SupportTicketPriority.High, response.Priority);

        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == response.CreatedTicketId);
        Assert.NotNull(ticket);
        Assert.Equal(SupportTicketPriority.High, ticket.Priority);
    }
}
