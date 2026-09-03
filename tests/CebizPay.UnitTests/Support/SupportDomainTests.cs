using CebizPay.Domain.Support.Entities;
using CebizPay.Domain.Support.Enums;
using Xunit;

namespace CebizPay.UnitTests.Support;

public class SupportDomainTests
{
    [Fact]
    public void CreateTicket_Enforces12HourReviewSlaDeadline()
    {
        var now = new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);
        var ticket = SupportTicket.Create(
            ticketNumber: "CBZ-SUP-2026-TEST01",
            userId: "usr_cust_01",
            organizationId: null,
            category: SupportTicketCategory.PaymentOrTransfer,
            subject: "Transfer dispute",
            description: "Money deducted but not delivered",
            priority: SupportTicketPriority.High,
            now: now);

        Assert.Equal("CBZ-SUP-2026-TEST01", ticket.TicketNumber);
        Assert.Equal("usr_cust_01", ticket.UserId);
        Assert.Null(ticket.OrganizationId);
        Assert.Equal(SupportTicketCategory.PaymentOrTransfer, ticket.Category);
        Assert.Equal(SupportTicketStatus.Open, ticket.Status);
        Assert.Equal(SupportTicketPriority.High, ticket.Priority);
        Assert.Equal(now, ticket.CreatedAtUtc);
        Assert.Equal(now.AddHours(12), ticket.SlaDueAtUtc);
        Assert.False(ticket.IsSlaBreached);
        Assert.Null(ticket.SlaBreachedAtUtc);
    }

    [Fact]
    public void CreateTicket_ThrowsArgumentException_WhenRequiredFieldsMissing()
    {
        var now = DateTime.UtcNow;

        Assert.Throws<ArgumentException>(() => SupportTicket.Create("", "u1", null, SupportTicketCategory.Other, "sub", "desc", SupportTicketPriority.Normal, now));
        Assert.Throws<ArgumentException>(() => SupportTicket.Create("NUM", "", null, SupportTicketCategory.Other, "sub", "desc", SupportTicketPriority.Normal, now));
        Assert.Throws<ArgumentException>(() => SupportTicket.Create("NUM", "u1", null, SupportTicketCategory.Other, "", "desc", SupportTicketPriority.Normal, now));
        Assert.Throws<ArgumentException>(() => SupportTicket.Create("NUM", "u1", null, SupportTicketCategory.Other, "sub", "", SupportTicketPriority.Normal, now));
    }

    [Fact]
    public void AddMessage_AppendsMessageToThread_AndUpdatesFirstResponseForAdmin()
    {
        var now = DateTime.UtcNow;
        var ticket = SupportTicket.Create("NUM-01", "u1", null, SupportTicketCategory.WalletOrAccount, "Balance issue", "Check balance", SupportTicketPriority.Normal, now);

        var custMsg = ticket.AddMessage("u1", TicketMessageSenderType.Customer, "Any update?", now.AddMinutes(10));
        Assert.Single(ticket.Messages);
        Assert.Null(ticket.FirstResponseAtUtc);

        var adminMsg = ticket.AddMessage("admin_01", TicketMessageSenderType.Admin, "Investigating now.", now.AddMinutes(20));
        Assert.Equal(2, ticket.Messages.Count);
        Assert.Equal(now.AddMinutes(20), ticket.FirstResponseAtUtc);
    }

    [Fact]
    public void AddMessage_ThrowsInvalidOperationException_WhenTicketClosedOrCancelled()
    {
        var now = DateTime.UtcNow;
        var ticket = SupportTicket.Create("NUM-02", "u1", null, SupportTicketCategory.Other, "Inquiry", "Hello", SupportTicketPriority.Normal, now);
        ticket.Close(now);

        Assert.Throws<InvalidOperationException>(() => ticket.AddMessage("u1", TicketMessageSenderType.Customer, "New message", now));

        var cancelledTicket = SupportTicket.Create("NUM-03", "u1", null, SupportTicketCategory.Other, "Inquiry", "Hello", SupportTicketPriority.Normal, now);
        cancelledTicket.Cancel(now);

        Assert.Throws<InvalidOperationException>(() => cancelledTicket.AddMessage("u1", TicketMessageSenderType.Customer, "New message", now));
    }

    [Fact]
    public void Lifecycle_OpenToEscalatedToInReviewToResolvedToClosed()
    {
        var now = DateTime.UtcNow;
        var ticket = SupportTicket.Create("NUM-04", "u1", null, SupportTicketCategory.SavingsOrThrift, "Thrift delay", "Payout late", SupportTicketPriority.Normal, now);

        ticket.Escalate(now.AddHours(1), "User requested operator attention");
        Assert.Equal(SupportTicketStatus.Escalated, ticket.Status);
        Assert.Equal(now.AddHours(1), ticket.EscalatedAtUtc);

        ticket.MarkInReview(now.AddHours(2));
        Assert.Equal(SupportTicketStatus.InReview, ticket.Status);

        ticket.Resolve("Thrift payout re-queued and completed successfully.", now.AddHours(3));
        Assert.Equal(SupportTicketStatus.Resolved, ticket.Status);
        Assert.Equal("Thrift payout re-queued and completed successfully.", ticket.ResolutionSummary);
        Assert.Equal(now.AddHours(3), ticket.ResolvedAtUtc);

        ticket.Close(now.AddHours(24));
        Assert.Equal(SupportTicketStatus.Closed, ticket.Status);
        Assert.Equal(now.AddHours(24), ticket.ClosedAtUtc);
    }

    [Fact]
    public void Resolve_ThrowsArgumentException_WhenResolutionSummaryIsEmpty()
    {
        var now = DateTime.UtcNow;
        var ticket = SupportTicket.Create("NUM-05", "u1", null, SupportTicketCategory.Other, "General", "Details", SupportTicketPriority.Normal, now);

        Assert.Throws<ArgumentException>(() => ticket.Resolve("", now));
        Assert.Throws<ArgumentException>(() => ticket.Resolve("   ", now));
    }

    [Fact]
    public void Reopen_ReopensResolvedTicket_AndClearsResolutionSummary()
    {
        var now = DateTime.UtcNow;
        var ticket = SupportTicket.Create("NUM-06", "u1", null, SupportTicketCategory.PaymentOrTransfer, "Transfer", "Details", SupportTicketPriority.Normal, now);
        ticket.Resolve("Resolved previously", now.AddHours(1));

        ticket.Reopen(now.AddHours(2));

        Assert.Equal(SupportTicketStatus.Open, ticket.Status);
        Assert.Null(ticket.ResolvedAtUtc);
        Assert.Null(ticket.ResolutionSummary);
    }

    [Fact]
    public void Reopen_ThrowsInvalidOperationException_WhenTicketNotResolved()
    {
        var now = DateTime.UtcNow;
        var ticket = SupportTicket.Create("NUM-07", "u1", null, SupportTicketCategory.PaymentOrTransfer, "Transfer", "Details", SupportTicketPriority.Normal, now);

        Assert.Throws<InvalidOperationException>(() => ticket.Reopen(now));
    }

    [Fact]
    public void MarkSlaBreached_IsIdempotent()
    {
        var now = DateTime.UtcNow;
        var ticket = SupportTicket.Create("NUM-08", "u1", null, SupportTicketCategory.PaymentOrTransfer, "Transfer", "Details", SupportTicketPriority.Normal, now);

        ticket.MarkSlaBreached(now.AddHours(13));
        Assert.True(ticket.IsSlaBreached);
        Assert.Equal(now.AddHours(13), ticket.SlaBreachedAtUtc);

        // Repeated call should not overwrite original breach time
        ticket.MarkSlaBreached(now.AddHours(15));
        Assert.True(ticket.IsSlaBreached);
        Assert.Equal(now.AddHours(13), ticket.SlaBreachedAtUtc);
    }
}
