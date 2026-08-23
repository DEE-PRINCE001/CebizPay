using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Thrift.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Thrift;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.IntegrationTests.Thrift;

/// <summary>
/// PostgreSQL Testcontainers integration tests verifying end-to-end Thrift (Ajo/Esusu) lifecycle:
/// group setup, invitations, unique position locking, 02:00 UTC wallet collection,
/// consecutive misses & delinquency suspension, pool payouts, and net contribution departing refunds.
/// </summary>
public sealed class ThriftIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public ThriftIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ApplicationDbContext> CreateDbContextAsync()
    {
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Fact]
    public async Task CompleteThriftLifecycle_EndToEnd_ShouldCollectPoolDisbursePayoutAndReimburseDepartingMember()
    {
        // 1. Arrange Services
        await using var dbContext = await CreateDbContextAsync();
        var ledgerPostingService = new LedgerPostingService(dbContext);
        var providerFactory = Substitute.For<IPaymentProviderFactory>();
        providerFactory.GetProvider(PaymentProvider.Flutterwave).Returns((IPaymentProvider?)null);

        var groupService = new ThriftGroupService(dbContext, ledgerPostingService);
        var collectionService = new ThriftCollectionService(dbContext, ledgerPostingService, providerFactory, NullLogger<ThriftCollectionService>.Instance);
        var payoutService = new ThriftPayoutService(dbContext, ledgerPostingService, NullLogger<ThriftPayoutService>.Instance);

        var user1Id = $"usr_thrf_1_{Guid.NewGuid():N}";
        var user2Id = $"usr_thrf_2_{Guid.NewGuid():N}";
        var user3Id = $"usr_thrf_3_{Guid.NewGuid():N}";

        var wallet1 = Wallet.CreateIndividualWallet(user1Id, Currency.NGN);
        var wallet2 = Wallet.CreateIndividualWallet(user2Id, Currency.NGN);
        var wallet3 = Wallet.CreateIndividualWallet(user3Id, Currency.NGN);

        // Fund user1 and user2 wallets with 200,000 NGN each. Leave user3 with 0 balance
        wallet1.Credit(200_000m);
        wallet2.Credit(200_000m);

        dbContext.Wallets.AddRange(wallet1, wallet2, wallet3);
        await dbContext.SaveChangesAsync();

        // 2. Create Thrift Group (3 positions, 50,000 NGN per cycle)
        var futureStartDate = DateTime.UtcNow.AddDays(7);
        var group = await groupService.CreateGroupAsync(
            user1Id,
            new Application.Common.Interfaces.Thrift.CreateThriftGroupRequest(
                null,
                "Team Alpha Ajo",
                "Workplace daily rotational group",
                Currency.NGN,
                50_000m,
                ThriftFrequency.Daily,
                3,
                futureStartDate,
                futureStartDate.AddDays(-2)));

        Assert.Equal(ThriftStatus.OpenForMembers, group.Status);

        // 3. Invite User2 and User3
        var invite2 = await groupService.InviteMemberAsync(group.Id, user1Id, new Application.Common.Interfaces.Thrift.InviteThriftMemberRequest("user2@test.com"));
        var invite3 = await groupService.InviteMemberAsync(group.Id, user1Id, new Application.Common.Interfaces.Thrift.InviteThriftMemberRequest("user3@test.com"));

        var member2 = await groupService.AcceptInvitationAsync(user2Id, new Application.Common.Interfaces.Thrift.AcceptThriftInvitationRequest(invite2.InvitationCode));
        var member3 = await groupService.AcceptInvitationAsync(user3Id, new Application.Common.Interfaces.Thrift.AcceptThriftInvitationRequest(invite3.InvitationCode));

        // 4. Select Positions (User1 -> Pos 1, User2 -> Pos 2, User3 -> Pos 3)
        await groupService.SelectPositionAsync(group.Id, user1Id, new Application.Common.Interfaces.Thrift.SelectThriftPositionRequest(1));
        await groupService.SelectPositionAsync(group.Id, user2Id, new Application.Common.Interfaces.Thrift.SelectThriftPositionRequest(2));
        await groupService.SelectPositionAsync(group.Id, user3Id, new Application.Common.Interfaces.Thrift.SelectThriftPositionRequest(3));

        // Refresh group state -> should be locked now
        var lockedGroup = await groupService.GetGroupByIdAsync(group.Id);
        Assert.Equal(ThriftStatus.Locked, lockedGroup!.Status);

        // 5. Start Cycle 1
        var groupEntity = await dbContext.ThriftGroups.Include(g => g.Members).FirstAsync(g => g.Id == group.Id);
        var now = DateTime.UtcNow;
        var cycle1 = groupEntity.StartCycle(1, now.AddDays(-1), now.AddDays(1), now);
        dbContext.ThriftCycles.Add(cycle1);
        await dbContext.SaveChangesAsync();

        // 6. Process Due Collections for Cycle 1
        var collected = await collectionService.ProcessDueCollectionsAsync(DateTime.UtcNow.AddMinutes(5));
        Assert.Equal(3, collected); // Processed 3 members: 2 successful, 1 missed

        // Check wallet balances:
        // User1: 200,000 - 50,000 = 150,000 NGN
        // User2: 200,000 - 50,000 = 150,000 NGN
        await dbContext.Entry(wallet1).ReloadAsync();
        await dbContext.Entry(wallet2).ReloadAsync();
        Assert.Equal(150_000m, wallet1.AvailableBalance);
        Assert.Equal(150_000m, wallet2.AvailableBalance);

        // 7. Process Cycle 1 Payout (Beneficiary = User1 with Position 1)
        var paidCount = await payoutService.ProcessReadyPayoutsAsync(DateTime.UtcNow.AddMinutes(5));
        Assert.Equal(1, paidCount);

        // User1 receives collected pool (100,000 NGN): 150,000 + 100,000 = 250,000 NGN
        await dbContext.Entry(wallet1).ReloadAsync();
        Assert.Equal(250_000m, wallet1.AvailableBalance);

        // 8. Process Cycle 2 Collection (User1 & User2 succeed, User3 misses 2nd consecutive -> suspended)
        var cycle2 = await dbContext.ThriftCycles.FirstAsync(c => c.CycleNumber == 2);
        var collectedCycle2 = await collectionService.ProcessDueCollectionsAsync(cycle2.DueDateUtc.AddMinutes(5));
        Assert.Equal(3, collectedCycle2);

        // Verify User3 is now Suspended
        var updatedMember3 = await dbContext.ThriftMembers.FirstAsync(m => m.Id == member3.Id);
        Assert.Equal(ThriftMemberStatus.Suspended, updatedMember3.Status);
        Assert.Equal(2, updatedMember3.ConsecutiveMissedCycles);

        // 9. Member 2 departs group and claims net refund
        // Member 2 contributed 100,000 NGN across Cycles 1 and 2, and received 0 payout.
        // Net refundable = 100,000 NGN
        var reimbursement = await groupService.RemoveAndReimburseMemberAsync(
            group.Id,
            member2.Id,
            user2Id,
            new Application.Common.Interfaces.Thrift.RemoveThriftMemberRequest("Leaving organization"));

        Assert.Equal(100_000m, reimbursement.NetRefundAmount);

        // User2 wallet balance: 100,000 (after 2 x 50,000 debits) + 100,000 (reimbursement) = 200,000 NGN
        await dbContext.Entry(wallet2).ReloadAsync();
        Assert.Equal(200_000m, wallet2.AvailableBalance);
    }
}
