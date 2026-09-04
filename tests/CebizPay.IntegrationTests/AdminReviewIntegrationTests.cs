using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.UseCases.Admins.GrantPermission;
using CebizPay.Application.UseCases.Admins.RevokePermission;
using CebizPay.Application.UseCases.Individuals.UpdateKycStatus;
using CebizPay.Application.UseCases.Organizations.ReviewKyb;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CebizPay.IntegrationTests;

/// <summary>
/// Integration tests for the KYC/KYB admin review workflow and permission delegation, including:
/// - Super Admin approves/rejects KYC
/// - Self-approval is blocked
/// - Rejection without reason is blocked
/// - Super Admin grants/revokes admin permissions with audit entries
/// </summary>
[Collection("Infrastructure")]
public sealed class AdminReviewIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public AdminReviewIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private ApplicationDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_fixture.PostgresContainer.GetConnectionString())
            .Options;
        return new ApplicationDbContext(opts);
    }

    // ─── KYC Review ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SuperAdmin_VerifiesKyc_ShouldUpdateStatusAndAuditLog()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();

        var userId = $"indiv_{Guid.NewGuid():N}";
        var adminId = $"superadmin_{Guid.NewGuid():N}";

        db.AdminProfiles.Add(new AdminProfile(adminId, AdminRoleType.SuperAdmin));
        var profile = new IndividualProfile(userId, "Alice", "Review");
        db.IndividualProfiles.Add(profile);

        var doc = new KycDocument(userId, DocumentType.Nimc, "NIMC-12345", "https://id.doc/url");
        db.KycDocuments.Add(doc);
        await db.SaveChangesAsync();

        var publisher = Substitute.For<IEventPublisher>();
        var handler = new UpdateKycStatusCommandHandler(db, publisher);
        var command = new UpdateKycStatusCommand(userId, KycStatus.Verified, adminId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Verified", result.KycStatus);

        var updatedProfile = await db.IndividualProfiles.FirstAsync(p => p.UserId == userId);
        Assert.Equal(KycStatus.Verified, updatedProfile.KycStatus);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a =>
            a.ActorId == adminId && a.Action == Domain.Auditing.AuditActions.KycVerified);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task SuperAdmin_RejectsKyc_WithReason_ShouldUpdateStatusAndAuditLog()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();

        var userId = $"indiv_{Guid.NewGuid():N}";
        var adminId = $"superadmin_{Guid.NewGuid():N}";
        const string rejectionReason = "Document expired.";

        db.AdminProfiles.Add(new AdminProfile(adminId, AdminRoleType.SuperAdmin));
        var profile = new IndividualProfile(userId, "Bob", "Rejected");
        db.IndividualProfiles.Add(profile);

        var doc = new KycDocument(userId, DocumentType.Nimc, "NIMC-12345", "https://id.doc/url");
        db.KycDocuments.Add(doc);
        await db.SaveChangesAsync();

        var publisher = Substitute.For<IEventPublisher>();
        var handler = new UpdateKycStatusCommandHandler(db, publisher);
        var command = new UpdateKycStatusCommand(userId, KycStatus.Rejected, adminId, rejectionReason);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Rejected", result.KycStatus);
        Assert.Equal(rejectionReason, result.Reason);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a =>
            a.ActorId == adminId && a.Action == Domain.Auditing.AuditActions.KycRejected);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task RejectKyc_WithoutReason_ShouldThrowValidationError()
    {
        var command = new UpdateKycStatusCommand(
            UserId: "user-x",
            NewStatus: KycStatus.Rejected,
            AdminUserId: "admin-x",
            Reason: null);

        var validator = new UpdateKycStatusCommandValidator();
        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Reason");
    }

    [Fact]
    public async Task SelfApproval_KycVerify_ShouldThrowInvalidOperation()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();

        var userId = $"user_{Guid.NewGuid():N}";
        db.AdminProfiles.Add(new AdminProfile(userId, AdminRoleType.SuperAdmin));
        var profile = new IndividualProfile(userId, "SelfApprove", "Test");
        db.IndividualProfiles.Add(profile);
        await db.SaveChangesAsync();

        var publisher = Substitute.For<IEventPublisher>();
        var handler = new UpdateKycStatusCommandHandler(db, publisher);

        // Admin trying to approve their own KYC (UserId == AdminUserId)
        var command = new UpdateKycStatusCommand(userId, KycStatus.Verified, userId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    // ─── KYB Review ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SuperAdmin_VerifiesKyb_ShouldUpdateOrganizationStatusAndAuditLog()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();

        var adminId = $"superadmin_{Guid.NewGuid():N}";
        db.AdminProfiles.Add(new AdminProfile(adminId, AdminRoleType.SuperAdmin));
        var org = new Organization("Acme Corp", $"acme_{Guid.NewGuid():N}@corp.com", "+2348000000001");
        org.CompleteStep2("RC123456", "https://logo.url", "https://cac.url");
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var publisher = Substitute.For<IEventPublisher>();
        var handler = new ReviewKybCommandHandler(db, publisher);
        var command = new ReviewKybCommand(org.Id, KybStatus.Verified, adminId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Verified", result.KybStatus);

        var updatedOrg = await db.Organizations.FirstAsync(o => o.Id == org.Id);
        Assert.Equal(KybStatus.Verified, updatedOrg.KybStatus);
        Assert.Equal(OrganizationStatus.Verified, updatedOrg.Status);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a =>
            a.ActorId == adminId && a.Action == Domain.Auditing.AuditActions.KybVerified);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task SuperAdmin_RejectsKyb_WithReason_ShouldUpdateAndAudit()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();

        var adminId = $"superadmin_{Guid.NewGuid():N}";
        db.AdminProfiles.Add(new AdminProfile(adminId, AdminRoleType.SuperAdmin));
        var org = new Organization("Beta Corp", $"beta_{Guid.NewGuid():N}@corp.com", "+2348000000002");
        org.CompleteStep2("RC654321", "https://beta.logo.url", "https://beta.cac.url");
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var publisher = Substitute.For<IEventPublisher>();
        var handler = new ReviewKybCommandHandler(db, publisher);
        var command = new ReviewKybCommand(org.Id, KybStatus.Rejected, adminId, "CAC certificate invalid.");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Rejected", result.KybStatus);
        Assert.Equal("CAC certificate invalid.", result.Reason);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a =>
            a.ActorId == adminId && a.Action == Domain.Auditing.AuditActions.KybRejected);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task RejectKyb_WithoutReason_ShouldFailValidation()
    {
        var command = new ReviewKybCommand(
            OrganizationId: Guid.NewGuid(),
            NewStatus: KybStatus.Rejected,
            AdminUserId: "admin-x",
            Reason: null);

        var validator = new ReviewKybCommandValidator();
        var result = await validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Reason");
    }

    // ─── Permission Delegation ───────────────────────────────────────────────

    [Fact]
    public async Task SuperAdmin_GrantsPermission_ShouldAddPermissionAndAudit()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();

        var superAdminUserId = $"super_{Guid.NewGuid():N}";
        var targetUserId = $"admin_{Guid.NewGuid():N}";

        var superAdmin = new AdminProfile(superAdminUserId, AdminRoleType.SuperAdmin);
        var targetAdmin = new AdminProfile(targetUserId, AdminRoleType.Admin);
        db.AdminProfiles.AddRange(superAdmin, targetAdmin);
        await db.SaveChangesAsync();

        var handler = new GrantAdminPermissionCommandHandler(db);
        var command = new GrantAdminPermissionCommand(superAdminUserId, targetAdmin.Id, Permissions.KycReview);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Contains(Permissions.KycReview, result.Permissions);

        var updatedAdmin = await db.AdminProfiles.FirstAsync(a => a.Id == targetAdmin.Id);
        Assert.True(updatedAdmin.HasPermission(Permissions.KycReview));

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a =>
            a.ActorId == superAdminUserId && a.Action == Domain.Auditing.AuditActions.AdminPermissionGranted);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task SuperAdmin_RevokesPermission_ShouldRemovePermissionAndAudit()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();

        var superAdminUserId = $"super_{Guid.NewGuid():N}";
        var targetUserId = $"admin_{Guid.NewGuid():N}";

        var superAdmin = new AdminProfile(superAdminUserId, AdminRoleType.SuperAdmin);
        var targetAdmin = new AdminProfile(targetUserId, AdminRoleType.Admin);
        targetAdmin.GrantPermission(Permissions.KycReview);
        db.AdminProfiles.AddRange(superAdmin, targetAdmin);
        await db.SaveChangesAsync();

        var handler = new RevokeAdminPermissionCommandHandler(db);
        var command = new RevokeAdminPermissionCommand(superAdminUserId, targetAdmin.Id, Permissions.KycReview);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.DoesNotContain(Permissions.KycReview, result.Permissions);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a =>
            a.ActorId == superAdminUserId && a.Action == Domain.Auditing.AuditActions.AdminPermissionRevoked);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task NonSuperAdmin_GrantsPermission_ShouldThrowUnauthorized()
    {
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();

        var regularAdminId = $"admin_{Guid.NewGuid():N}";
        var targetAdminId = $"admin_{Guid.NewGuid():N}";

        var regularAdmin = new AdminProfile(regularAdminId, AdminRoleType.Admin);
        var targetAdmin = new AdminProfile(targetAdminId, AdminRoleType.Admin);
        db.AdminProfiles.AddRange(regularAdmin, targetAdmin);
        await db.SaveChangesAsync();

        var handler = new GrantAdminPermissionCommandHandler(db);
        var command = new GrantAdminPermissionCommand(regularAdminId, targetAdmin.Id, Permissions.KycReview);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(command, CancellationToken.None));
    }
}
