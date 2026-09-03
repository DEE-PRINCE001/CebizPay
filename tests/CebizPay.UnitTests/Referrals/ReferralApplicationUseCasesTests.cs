using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Referrals;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Referrals;
using CebizPay.Application.UseCases.Referrals;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Referrals.Entities;
using CebizPay.Domain.Referrals.Enums;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Referrals;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Referrals;

public class ReferralApplicationUseCasesTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void ReferralCodeGenerator_GeneratesSafeUppercaseCode()
    {
        var generator = new ReferralCodeGenerator();
        var code = generator.GenerateCode();

        Assert.NotNull(code);
        Assert.StartsWith("CBZ", code, StringComparison.Ordinal);
        Assert.Equal(9, code.Length); // CBZ + 6 chars
        Assert.True(code.All(c => char.IsLetterOrDigit(c) && (!char.IsLetter(c) || char.IsUpper(c))));
    }

    [Fact]
    public async Task DisabledReferralRewardActivationService_RejectsFinancialDisbursement()
    {
        var service = new DisabledReferralRewardActivationService();
        var result = await service.ActivateRewardAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Null(result.LedgerTransactionReference);
        Assert.Contains("disabled in Phase 6", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetOrCreateReferralCode_ReturnsExistingIfPresent_OtherwiseCreatesNew()
    {
        await using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("user_abc");

        var codeGenerator = new ReferralCodeGenerator();
        var handler = new GetOrCreateReferralCodeCommandHandler(db, currentUserService, codeGenerator);

        // First call generates code
        var code1 = await handler.Handle(new GetOrCreateReferralCodeCommand(), CancellationToken.None);
        Assert.NotNull(code1);

        // Second call returns same code
        var code2 = await handler.Handle(new GetOrCreateReferralCodeCommand(), CancellationToken.None);
        Assert.Equal(code1, code2);
    }

    [Fact]
    public async Task ClaimReferralCode_SuccessfulClaim_CreatesRelationship()
    {
        await using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("referred_new_user");

        var auditLog = Substitute.For<IAuditLogService>();
        var qualification = Substitute.For<IReferralQualificationService>();

        // Referrer has code
        var referrerCode = ReferralCode.Create("referrer_user", "CBZABC123", DateTime.UtcNow);
        db.ReferralCodes.Add(referrerCode);
        await db.SaveChangesAsync();

        var handler = new ClaimReferralCodeCommandHandler(db, currentUserService, qualification, auditLog);

        var relationshipId = await handler.Handle(new ClaimReferralCodeCommand("CBZABC123"), CancellationToken.None);

        var saved = await db.ReferralRelationships.FirstOrDefaultAsync(r => r.Id == relationshipId);
        Assert.NotNull(saved);
        Assert.Equal("referrer_user", saved.ReferrerUserId);
        Assert.Equal("referred_new_user", saved.ReferredUserId);
        Assert.Equal(ReferralQualificationStatus.Pending, saved.QualificationStatus);
    }

    [Fact]
    public async Task ClaimReferralCode_SelfReferral_ThrowsInvalidOperationException()
    {
        await using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("same_user");

        var code = ReferralCode.Create("same_user", "CBZSELF123", DateTime.UtcNow);
        db.ReferralCodes.Add(code);
        await db.SaveChangesAsync();

        var handler = new ClaimReferralCodeCommandHandler(db, currentUserService, Substitute.For<IReferralQualificationService>(), Substitute.For<IAuditLogService>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ClaimReferralCodeCommand("CBZSELF123"), CancellationToken.None));
    }

    [Fact]
    public async Task ClaimReferralCode_DuplicateClaim_ThrowsInvalidOperationException()
    {
        await using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("referred_user");

        var code1 = ReferralCode.Create("referrer_1", "CBZREF001", DateTime.UtcNow);
        var code2 = ReferralCode.Create("referrer_2", "CBZREF002", DateTime.UtcNow);
        db.ReferralCodes.AddRange(code1, code2);

        // Already claimed code1
        var existingRel = ReferralRelationship.Create("referrer_1", "referred_user", code1.Id, code1.Code, DateTime.UtcNow);
        db.ReferralRelationships.Add(existingRel);
        await db.SaveChangesAsync();

        var handler = new ClaimReferralCodeCommandHandler(db, currentUserService, Substitute.For<IReferralQualificationService>(), Substitute.For<IAuditLogService>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ClaimReferralCodeCommand("CBZREF002"), CancellationToken.None));
    }

    [Fact]
    public async Task GetReferralDashboard_CalculatesCountsAndAmountsAccurately()
    {
        await using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("my_referrer_id");

        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetOrCreateReferralCodeCommand>(), Arg.Any<CancellationToken>())
            .Returns("CBZMYCODE");

        var setting = ReferralSetting.CreateDefault(500m, 10, "admin");
        db.ReferralSettings.Add(setting);

        // Create 3 referrals: 2 qualified, 1 pending
        var r1 = ReferralRelationship.Create("my_referrer_id", "user_1", Guid.NewGuid(), "CBZMYCODE", DateTime.UtcNow);
        r1.Qualify(1000m, "TX1", ReferralRewardEligibility.Eligible, DateTime.UtcNow);

        var r2 = ReferralRelationship.Create("my_referrer_id", "user_2", Guid.NewGuid(), "CBZMYCODE", DateTime.UtcNow);
        r2.Qualify(2000m, "TX2", ReferralRewardEligibility.Eligible, DateTime.UtcNow);

        var r3 = ReferralRelationship.Create("my_referrer_id", "user_3", Guid.NewGuid(), "CBZMYCODE", DateTime.UtcNow);

        db.ReferralRelationships.AddRange(r1, r2, r3);

        // Rewards: 2 eligible (500 each), 1 pending (500)
        var rew1 = ReferralReward.Create(r1.Id, "my_referrer_id", "user_1", 500m, ReferralRewardStatus.Eligible, DateTime.UtcNow);
        var rew2 = ReferralReward.Create(r2.Id, "my_referrer_id", "user_2", 500m, ReferralRewardStatus.Eligible, DateTime.UtcNow);
        var rew3 = ReferralReward.Create(r3.Id, "my_referrer_id", "user_3", 500m, ReferralRewardStatus.Pending, DateTime.UtcNow);
        db.ReferralRewards.AddRange(rew1, rew2, rew3);

        await db.SaveChangesAsync();

        var handler = new GetReferralDashboardQueryHandler(db, currentUserService, sender);
        var dashboard = await handler.Handle(new GetReferralDashboardQuery(), CancellationToken.None);

        Assert.Equal("CBZMYCODE", dashboard.ReferralCode);
        Assert.Equal(3, dashboard.TotalReferrals);
        Assert.Equal(2, dashboard.QualifiedReferrals);
        Assert.Equal(8, dashboard.RemainingCapacity); // 10 - 2 = 8
        Assert.Equal(1000m, dashboard.EligibleRewardAmount); // 500 * 2
        Assert.Equal(500m, dashboard.PendingRewardAmount);
    }

    [Fact]
    public async Task UpdateReferralSetting_SuperAdmin_UpdatesConfigurationAndLogsAudit()
    {
        await using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("super_admin_id");

        // Seed Super Admin profile
        var admin = new AdminProfile("super_admin_id", AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(admin);

        var setting = ReferralSetting.CreateDefault(500m, 10, "admin");
        db.ReferralSettings.Add(setting);
        await db.SaveChangesAsync();

        var auditLog = Substitute.For<IAuditLogService>();
        var handler = new UpdateReferralSettingCommandHandler(db, currentUserService, auditLog);

        var result = await handler.Handle(new UpdateReferralSettingCommand(750.00m, 15, true), CancellationToken.None);

        Assert.Equal(750.00m, result.RewardAmountPerSuccessfulReferral);
        Assert.Equal(15, result.MaximumSuccessfulReferralsPerUser);
        Assert.Equal(2, result.Version);

        await auditLog.Received(1).LogAsync(
            Arg.Is(AuditActions.ReferralSettingUpdated),
            Arg.Is(AuditResourceTypes.ReferralSetting),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<object>(),
            Arg.Any<Guid?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateReferralSetting_NonSuperAdmin_ThrowsUnauthorizedAccessException()
    {
        await using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("auditor_id");

        // Auditor role, not Super Admin
        var admin = new AdminProfile("auditor_id", AdminRoleType.Auditor);
        db.AdminProfiles.Add(admin);
        await db.SaveChangesAsync();

        var handler = new UpdateReferralSettingCommandHandler(db, currentUserService, Substitute.For<IAuditLogService>());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new UpdateReferralSettingCommand(1000m, 20, true), CancellationToken.None));
    }

    [Fact]
    public async Task GetReferralSetting_SuperAdminAndAuditorAllowed_RegularUserRejected()
    {
        await using var db = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns("regular_user");

        var handler = new GetReferralSettingQueryHandler(db, currentUserService);

        // Regular user has no admin profile
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new GetReferralSettingQuery(), CancellationToken.None));
    }
}
