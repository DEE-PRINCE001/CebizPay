using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Support.Entities;
using CebizPay.Domain.Support.Enums;
using Xunit;

namespace CebizPay.UnitTests.BusinessLogic;

public sealed class StateTransitionBoundaryTests
{
    [Fact]
    public void LedgerTransaction_Completed_CannotTransitionToFailedOrReversedDirectly()
    {
        var txn = new LedgerTransaction(LedgerTransactionType.PeerTransfer);
        var now = DateTime.UtcNow;

        txn.Complete(now);
        Assert.Equal(LedgerTransactionStatus.Completed, txn.Status);

        // Cannot complete twice (idempotent no-op)
        txn.Complete(now.AddSeconds(5));
        Assert.Equal(now, txn.CompletedAtUtc);

        // Reversal requires explicit MarkReversed method
        txn.MarkReversed(now.AddMinutes(1));
        Assert.Equal(LedgerTransactionStatus.Reversed, txn.Status);

        // Cannot reverse twice
        Assert.Throws<InvalidOperationException>(() => txn.MarkReversed(now.AddMinutes(2)));

        // Cannot complete a reversed transaction
        Assert.Throws<InvalidOperationException>(() => txn.Complete(now.AddMinutes(3)));
    }

    [Fact]
    public void LedgerTransaction_Pending_CannotBeReversedDirectly()
    {
        var txn = new LedgerTransaction(LedgerTransactionType.BankTransfer);
        var now = DateTime.UtcNow;

        Assert.Equal(LedgerTransactionStatus.Pending, txn.Status);
        Assert.Throws<InvalidOperationException>(() => txn.MarkReversed(now));
    }

    [Fact]
    public void PaymentAttempt_TerminalStates_PreventIllegalTransitions()
    {
        var attempt = PaymentAttempt.Create(
            Guid.NewGuid(),
            PaymentProvider.Paystack,
            1,
            "CBZPA-TST-001",
            1000m,
            Currency.NGN);

        Assert.Equal(PaymentAttemptStatus.Created, attempt.Status);

        // Cannot jump directly from Created to Succeeded
        Assert.Throws<InvalidOperationException>(() => attempt.MarkSucceeded("PSTK-REF-1"));

        // Transition to Processing
        attempt.MarkProcessing();
        Assert.Equal(PaymentAttemptStatus.Processing, attempt.Status);

        // Cannot re-transition to Processing
        Assert.Throws<InvalidOperationException>(() => attempt.MarkProcessing());

        // Transition to Succeeded (terminal)
        attempt.MarkSucceeded("PSTK-REF-1");
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);

        // Succeeded cannot transition back to Processing or Failed
        Assert.Throws<InvalidOperationException>(() => attempt.MarkProcessing());
        Assert.Throws<InvalidOperationException>(() => attempt.MarkFailed("ERR", "Provider timeout"));
        Assert.Throws<InvalidOperationException>(() => attempt.MarkCancelled("User cancelled"));
    }

    [Fact]
    public void PaymentAttempt_UnknownState_AllowsResolutionToSucceededOrFailed()
    {
        var attempt = PaymentAttempt.Create(
            Guid.NewGuid(),
            PaymentProvider.Flutterwave,
            1,
            "CBZPA-TST-002",
            5000m,
            Currency.NGN);

        attempt.MarkProcessing();
        attempt.MarkUnknown("Network timeout during provider POST");
        Assert.Equal(PaymentAttemptStatus.Unknown, attempt.Status);

        // Unknown can transition to Succeeded upon subsequent webhook/reconciliation
        attempt.MarkSucceeded("FLW-REF-RESOLVED");
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
    }

    [Fact]
    public void SupportTicket_ClosedTicket_CannotBeReopenedOrMutated()
    {
        var now = DateTime.UtcNow;
        var ticket = SupportTicket.Create(
            "CBZ-SUP-2026-0001",
            "user-1",
            null,
            SupportTicketCategory.Other,
            "Issue Subject",
            "Initial problem statement",
            SupportTicketPriority.Normal,
            now);

        Assert.Equal(SupportTicketStatus.Open, ticket.Status);

        ticket.Close(now);
        Assert.Equal(SupportTicketStatus.Closed, ticket.Status);

        // Closed ticket cannot be reopened (must open a new ticket)
        Assert.Throws<InvalidOperationException>(() => ticket.Reopen(now));

        // Closed ticket cannot accept new messages
        Assert.Throws<InvalidOperationException>(() => ticket.AddMessage("user-1", TicketMessageSenderType.Customer, "More info", now));

        // Closed ticket cannot be escalated
        Assert.Throws<InvalidOperationException>(() => ticket.Escalate(now));
    }

    [Fact]
    public void SupportTicket_ResolvedTicket_CanBeReopenedByCustomer()
    {
        var now = DateTime.UtcNow;
        var ticket = SupportTicket.Create(
            "CBZ-SUP-2026-0002",
            "user-1",
            null,
            SupportTicketCategory.PaymentOrTransfer,
            "Dispute",
            "Dispute details",
            SupportTicketPriority.High,
            now);

        ticket.Resolve("Resolved with refund", now);
        Assert.Equal(SupportTicketStatus.Resolved, ticket.Status);

        ticket.Reopen(now.AddHours(1));
        Assert.Equal(SupportTicketStatus.Open, ticket.Status);
        Assert.Null(ticket.ResolvedAtUtc);
        Assert.Null(ticket.ResolutionSummary);
    }

    [Fact]
    public void SupportTicket_SlaBreached_IsIdempotent()
    {
        var now = DateTime.UtcNow;
        var ticket = SupportTicket.Create(
            "CBZ-SUP-2026-0003",
            "user-1",
            null,
            SupportTicketCategory.WalletOrAccount,
            "Login issue",
            "Cannot log in",
            SupportTicketPriority.Critical,
            now);

        ticket.MarkSlaBreached(now.AddHours(13));
        Assert.True(ticket.IsSlaBreached);
        var breachTime = ticket.SlaBreachedAtUtc;

        // Idempotent: repeated calls do not alter breach timestamp
        ticket.MarkSlaBreached(now.AddHours(14));
        Assert.Equal(breachTime, ticket.SlaBreachedAtUtc);
    }

    [Fact]
    public void AdminInvitation_LifecycleAndExpiration_EnforceStrictBoundaries()
    {
        var now = DateTime.UtcNow;
        var invite = new AdminInvitation(
            "admin.new@cebizpay.com",
            AdminRoleType.Admin,
            "hash123",
            "superadmin-1",
            TimeSpan.FromHours(24));

        Assert.Equal(AdminInvitationStatus.Pending, invite.Status);
        Assert.False(invite.IsExpired(now));

        // One tick before expiration is valid
        var oneTickBefore = invite.ExpiresAtUtc.AddTicks(-1);
        Assert.False(invite.IsExpired(oneTickBefore));

        // One tick after expiration is expired
        var oneTickAfter = invite.ExpiresAtUtc.AddTicks(1);
        Assert.True(invite.IsExpired(oneTickAfter));

        // Attempting to redeem expired invitation throws and marks expired
        Assert.Throws<InvalidOperationException>(() => invite.Redeem("user-created", oneTickAfter));
        Assert.Equal(AdminInvitationStatus.Expired, invite.Status);
    }

    [Fact]
    public void AdminInvitation_Redeemed_CannotBeRedeemedAgain()
    {
        var now = DateTime.UtcNow;
        var invite = new AdminInvitation(
            "admin2@cebizpay.com",
            AdminRoleType.Auditor,
            "hash456",
            "superadmin-1");

        invite.Redeem("admin-user-id", now);
        Assert.Equal(AdminInvitationStatus.Redeemed, invite.Status);

        Assert.Throws<InvalidOperationException>(() => invite.Redeem("second-user-id", now.AddMinutes(1)));
    }
}
