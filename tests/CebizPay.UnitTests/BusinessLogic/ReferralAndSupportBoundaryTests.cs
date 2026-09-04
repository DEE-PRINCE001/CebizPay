using CebizPay.Domain.Referrals.Entities;
using CebizPay.Domain.Support.Entities;
using CebizPay.Domain.Support.Enums;
using CebizPay.Infrastructure.Referrals;
using Xunit;

namespace CebizPay.UnitTests.BusinessLogic;

public sealed class ReferralAndSupportBoundaryTests
{
    [Fact]
    public async Task Referral_RewardActivation_IsStrictlyDisabledInPhase6()
    {
        var activationService = new DisabledReferralRewardActivationService();
        var rewardId = Guid.NewGuid();

        var result = await activationService.ActivateRewardAsync(rewardId);

        Assert.False(result.Succeeded);
        Assert.Null(result.LedgerTransactionReference);
        Assert.Contains("disabled in Phase 6", result.Message);
    }

    [Fact]
    public void ReferralSetting_CapacityAndReward_EnforcesBoundaries()
    {
        var setting = ReferralSetting.CreateDefault(
            defaultReward: 500.00m,
            defaultMaxReferrals: 10,
            createdBy: "admin-1");

        Assert.Equal(500.00m, setting.RewardAmountPerSuccessfulReferral);
        Assert.Equal(10, setting.MaximumSuccessfulReferralsPerUser);
        Assert.True(setting.IsActive);

        // Update with custom cap
        setting.Update(
            rewardAmount: 250.00m,
            maximumReferrals: 5,
            isActive: true,
            updatedBy: "admin-1",
            now: DateTime.UtcNow);

        Assert.Equal(250.00m, setting.RewardAmountPerSuccessfulReferral);
        Assert.Equal(5, setting.MaximumSuccessfulReferralsPerUser);
    }

    [Fact]
    public void SupportTicket_SlaCalculation_EnforcesExact12HourReviewWindow()
    {
        var created = new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc);
        var ticket = SupportTicket.Create(
            "CBZ-SUP-2026-9999",
            "customer-user-1",
            null,
            SupportTicketCategory.PaymentOrTransfer,
            "Billing query",
            "Payment charged twice",
            SupportTicketPriority.High,
            created);

        // Authoritative 12-hour SLA deadline
        var expectedDue = new DateTime(2026, 9, 4, 22, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedDue, ticket.SlaDueAtUtc);
        Assert.False(ticket.IsSlaBreached);

        // At 11 hours 59 minutes: SLA not breached
        var beforeBreach = created.AddHours(11).AddMinutes(59);
        Assert.True(beforeBreach < ticket.SlaDueAtUtc);

        // At 12 hours 1 minute: SLA breached
        var afterBreach = created.AddHours(12).AddMinutes(1);
        Assert.True(afterBreach > ticket.SlaDueAtUtc);

        ticket.MarkSlaBreached(afterBreach);
        Assert.True(ticket.IsSlaBreached);
        Assert.Equal(afterBreach, ticket.SlaBreachedAtUtc);
    }

    [Fact]
    public void SupportTicket_MessageThreading_PreservesParticipantHierarchy()
    {
        var now = DateTime.UtcNow;
        var ticket = SupportTicket.Create(
            "CBZ-SUP-2026-8888",
            "customer-1",
            null,
            SupportTicketCategory.Other,
            "Crash on transfer",
            "App crashes when clicking send",
            SupportTicketPriority.Critical,
            now);

        // Kola bot automated triage message
        var msg1 = ticket.AddMessage(null, TicketMessageSenderType.Kola, "Hello! I am Kola, your assistant.", now.AddSeconds(1));
        Assert.Equal(TicketMessageSenderType.Kola, msg1.SenderType);

        // User response
        var msg2 = ticket.AddMessage("customer-1", TicketMessageSenderType.Customer, "Here is the screenshot", now.AddSeconds(10));
        Assert.Equal("customer-1", msg2.SenderUserId);
        Assert.Equal(TicketMessageSenderType.Customer, msg2.SenderType);

        // Admin agent first response records FirstResponseAtUtc
        Assert.Null(ticket.FirstResponseAtUtc);
        var adminTime = now.AddMinutes(5);
        var msg3 = ticket.AddMessage("admin-1", TicketMessageSenderType.Admin, "Investigating now.", adminTime);
        Assert.Equal(TicketMessageSenderType.Admin, msg3.SenderType);
        Assert.Equal(adminTime, ticket.FirstResponseAtUtc);

        // Second admin response does not overwrite FirstResponseAtUtc
        ticket.AddMessage("admin-1", TicketMessageSenderType.Admin, "Issue identified.", adminTime.AddMinutes(10));
        Assert.Equal(adminTime, ticket.FirstResponseAtUtc);
    }
}
