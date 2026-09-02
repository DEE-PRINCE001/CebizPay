using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Admin.ThriftOversight;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Permissions;
using CebizPay.Domain.Thrift.Entities;
using CebizPay.Domain.Thrift.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Admin;

public sealed class AdminThriftOversightUseCasesTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ThriftGroup CreateTestGroup(string creatorId = "creator-1")
    {
        return ThriftGroup.Create(
            organizationId: null,
            creatorUserId: creatorId,
            name: "Savings Circle Alpha",
            description: "Test group",
            currency: Currency.NGN,
            contributionAmount: 50000m,
            frequency: ThriftFrequency.Monthly,
            totalPositions: 3,
            startDateUtc: DateTime.UtcNow.AddDays(5),
            positionSelectionDeadlineUtc: DateTime.UtcNow.AddDays(3));
    }

    [Fact]
    public async Task GetAdminThriftDirectory_AsSuperAdmin_ShouldReturnPagedGroups()
    {
        await using var db = CreateDbContext();
        var superAdminId = "super-1";
        var superAdminProfile = new AdminProfile(superAdminId, AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(superAdminProfile);

        var group1 = CreateTestGroup("creator-1");
        var group2 = CreateTestGroup("creator-2");
        db.ThriftGroups.AddRange(group1, group2);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(superAdminId);

        var handler = new GetAdminThriftDirectoryQueryHandler(db, currentUserService);
        var query = new GetAdminThriftDirectoryQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(150000m, result.Items[0].TotalPoolVolume); // 3 * 50000
    }

    [Fact]
    public async Task GetAdminThriftDelinquencies_ShouldFilterSuspendedOrMissedCycleMembers()
    {
        await using var db = CreateDbContext();
        var superAdminId = "super-1";
        var superAdminProfile = new AdminProfile(superAdminId, AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(superAdminProfile);

        var group = CreateTestGroup("creator-1");
        var member1 = group.AddMember("user-delinquent");
        member1.RecordMissedContribution();
        member1.RecordMissedContribution(); // 2 misses -> Suspended

        var member2 = group.AddMember("user-good");

        db.ThriftGroups.Add(group);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(superAdminId);

        var handler = new GetAdminThriftDelinquenciesQueryHandler(db, currentUserService);
        var query = new GetAdminThriftDelinquenciesQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("user-delinquent", result.Items[0].UserId);
        Assert.Equal(2, result.Items[0].ConsecutiveMissedCycles);
        Assert.Equal("Suspended", result.Items[0].Status);
    }

    [Fact]
    public async Task PauseAndResumeThriftGroup_AsSuperAdmin_ShouldTransitionStatusAndAudit()
    {
        await using var db = CreateDbContext();
        var superAdminId = "super-1";
        var superAdminProfile = new AdminProfile(superAdminId, AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(superAdminProfile);

        var group = CreateTestGroup();
        db.ThriftGroups.Add(group);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(superAdminId);

        var pauseHandler = new PauseThriftGroupCommandHandler(db, currentUserService);
        var pauseCommand = new PauseThriftGroupCommand(group.Id, "Investigating payment provider dispute");

        var pauseResult = await pauseHandler.Handle(pauseCommand, CancellationToken.None);
        Assert.True(pauseResult);

        var pausedGroup = await db.ThriftGroups.FirstOrDefaultAsync(g => g.Id == group.Id);
        Assert.Equal(ThriftStatus.Paused, pausedGroup!.Status);

        // Resume group
        var resumeHandler = new ResumeThriftGroupCommandHandler(db, currentUserService);
        var resumeCommand = new ResumeThriftGroupCommand(group.Id);

        var resumeResult = await resumeHandler.Handle(resumeCommand, CancellationToken.None);
        Assert.True(resumeResult);

        var resumedGroup = await db.ThriftGroups.FirstOrDefaultAsync(g => g.Id == group.Id);
        Assert.Equal(ThriftStatus.Locked, resumedGroup!.Status);
    }

    [Fact]
    public async Task CreateAndResolveDispute_ShouldUpdateStatusAndLogAudit()
    {
        await using var db = CreateDbContext();
        var superAdminId = "super-1";
        var superAdminProfile = new AdminProfile(superAdminId, AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(superAdminProfile);

        var group = CreateTestGroup();
        db.ThriftGroups.Add(group);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("user-complaining");

        var createHandler = new CreateThriftDisputeCommandHandler(db, currentUserService);
        var createCommand = new CreateThriftDisputeCommand(group.Id, null, null, "Missed cycle payout dispute");

        var disputeDto = await createHandler.Handle(createCommand, CancellationToken.None);
        Assert.NotEqual(Guid.Empty, disputeDto.Id);
        Assert.Equal("Open", disputeDto.Status);

        // Super Admin resolves dispute
        currentUserService.UserId.Returns(superAdminId);
        var resolveHandler = new ResolveThriftDisputeCommandHandler(db, currentUserService);
        var resolveCommand = new ResolveThriftDisputeCommand(disputeDto.Id, "Dispute investigated and resolved with manual settlement.", false);

        var resolvedDto = await resolveHandler.Handle(resolveCommand, CancellationToken.None);
        Assert.Equal("Resolved", resolvedDto.Status);
        Assert.Equal("Dispute investigated and resolved with manual settlement.", resolvedDto.ResolutionNotes);
        Assert.Equal(superAdminId, resolvedDto.ResolvedByUserId);
    }
}
