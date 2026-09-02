using System.Security.Cryptography;
using System.Text;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Admin.Manage;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Admin;

public sealed class AdminManageUseCasesTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task InviteAdmin_AsSuperAdmin_ShouldCreateInvitationAndDispatchEmail()
    {
        await using var db = CreateDbContext();
        var superAdminId = "super-admin-1";

        var superAdminProfile = new AdminProfile(superAdminId, AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(superAdminProfile);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(superAdminId);

        var identityService = Substitute.For<IIdentityService>();
        identityService.FindUserByEmailAsync("newadmin@cebizpay.com", Arg.Any<CancellationToken>())
            .Returns((false, string.Empty, string.Empty, null));

        var emailService = Substitute.For<IEmailService>();

        var handler = new InviteAdminCommandHandler(db, currentUserService, identityService, emailService);
        var command = new InviteAdminCommand("newadmin@cebizpay.com", AdminRoleType.Admin);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.InvitationId);
        Assert.Equal("newadmin@cebizpay.com", result.Email);
        Assert.Equal("Admin", result.Role);
        Assert.False(string.IsNullOrWhiteSpace(result.InvitationToken));

        var savedInvite = await db.AdminInvitations.FirstOrDefaultAsync(i => i.Id == result.InvitationId);
        Assert.NotNull(savedInvite);
        Assert.Equal(AdminInvitationStatus.Pending, savedInvite.Status);

        // Verify raw token is not stored in DB directly (only SHA-256 hash)
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.InvitationToken))).ToLowerInvariant();
        Assert.Equal(tokenHash, savedInvite.TokenHash);

        // Verify email was sent
        await emailService.Received(1).SendEmailAsync(
            "newadmin@cebizpay.com",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InviteAdmin_AsAuditor_ShouldThrowUnauthorizedAccessException()
    {
        await using var db = CreateDbContext();
        var auditorId = "auditor-1";

        var auditorProfile = new AdminProfile(auditorId, AdminRoleType.Auditor);
        db.AdminProfiles.Add(auditorProfile);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(auditorId);

        var identityService = Substitute.For<IIdentityService>();
        var handler = new InviteAdminCommandHandler(db, currentUserService, identityService, null);

        var command = new InviteAdminCommand("target@cebizpay.com", AdminRoleType.Admin);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task RedeemAdminInvite_ValidToken_ShouldCreateAdminAndRedeem()
    {
        await using var db = CreateDbContext();
        var superAdminId = "super-admin-1";

        var rawToken = "my-secure-crypto-invitation-token-123456";
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

        var invitation = new AdminInvitation("redeemed@cebizpay.com", AdminRoleType.Admin, tokenHash, superAdminId);
        db.AdminInvitations.Add(invitation);
        await db.SaveChangesAsync();

        var identityService = Substitute.For<IIdentityService>();
        identityService.FindUserByEmailAsync("redeemed@cebizpay.com", Arg.Any<CancellationToken>())
            .Returns((false, string.Empty, string.Empty, null));
        identityService.RegisterUserAsync("redeemed@cebizpay.com", "SecurePassword123!", null, Arg.Any<CancellationToken>())
            .Returns((true, "new-admin-user-id", Array.Empty<string>()));
        identityService.IssueTokensForUserAsync("new-admin-user-id", Arg.Any<CancellationToken>())
            .Returns(("access-jwt-token", "refresh-jwt-token"));

        var handler = new RedeemAdminInviteCommandHandler(db, identityService);
        var command = new RedeemAdminInviteCommand(rawToken, "SecurePassword123!");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("new-admin-user-id", result.UserId);
        Assert.Equal("redeemed@cebizpay.com", result.Email);
        Assert.Equal("Admin", result.Role);
        Assert.Equal("access-jwt-token", result.AccessToken);

        // Verify AdminProfile created in DB
        var adminProfile = await db.AdminProfiles.FirstOrDefaultAsync(a => a.UserId == "new-admin-user-id");
        Assert.NotNull(adminProfile);
        Assert.Equal(AdminRoleType.Admin, adminProfile.Role);
        Assert.True(adminProfile.IsActive);
        Assert.False(adminProfile.IsDeleted);

        // Verify invitation marked Redeemed
        var updatedInvite = await db.AdminInvitations.FirstOrDefaultAsync(i => i.Id == invitation.Id);
        Assert.Equal(AdminInvitationStatus.Redeemed, updatedInvite!.Status);
        Assert.Equal("new-admin-user-id", updatedInvite.RedeemedByUserId);
    }

    [Fact]
    public async Task RedeemAdminInvite_ExpiredToken_ShouldThrowInvalidOperationException()
    {
        await using var db = CreateDbContext();
        var rawToken = "expired-token";
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

        var invitation = new AdminInvitation("expired@cebizpay.com", AdminRoleType.Admin, tokenHash, "super-admin", TimeSpan.FromMinutes(-10));
        db.AdminInvitations.Add(invitation);
        await db.SaveChangesAsync();

        var identityService = Substitute.For<IIdentityService>();
        var handler = new RedeemAdminInviteCommandHandler(db, identityService);

        var command = new RedeemAdminInviteCommand(rawToken, "Password123!");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ToggleAdminStatus_SelfDeactivation_ShouldThrowInvalidOperationException()
    {
        await using var db = CreateDbContext();
        var superAdminId = "super-1";
        var profile = new AdminProfile(superAdminId, AdminRoleType.SuperAdmin);
        db.AdminProfiles.Add(profile);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(superAdminId);

        var identityService = Substitute.For<IIdentityService>();
        var handler = new ToggleAdminStatusCommandHandler(db, currentUserService, identityService);

        var command = new ToggleAdminStatusCommand(profile.Id, false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("cannot deactivate their own", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAdmin_LastActiveSuperAdmin_ShouldThrowInvalidOperationException()
    {
        await using var db = CreateDbContext();
        var superAdmin1Id = "super-1";
        var superAdmin2Id = "super-2";

        var profile1 = new AdminProfile(superAdmin1Id, AdminRoleType.SuperAdmin);
        var profile2 = new AdminProfile(superAdmin2Id, AdminRoleType.SuperAdmin);
        db.AdminProfiles.AddRange(profile1, profile2);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(superAdmin1Id);

        var handler = new DeleteAdminCommandHandler(db, currentUserService);

        // Deleting super2 leaves only 1 active superadmin
        var delete2Command = new DeleteAdminCommand(profile2.Id);
        var result = await handler.Handle(delete2Command, CancellationToken.None);
        Assert.True(result);

        // Deleting self is blocked
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new DeleteAdminCommand(profile1.Id), CancellationToken.None));
    }

    [Fact]
    public async Task GetAdminDirectory_ShouldExcludeSoftDeletedAdmins()
    {
        await using var db = CreateDbContext();
        var superAdminId = "super-1";
        var activeSuper = new AdminProfile(superAdminId, AdminRoleType.SuperAdmin);
        var activeAdmin = new AdminProfile("admin-active", AdminRoleType.Admin);
        var deletedAdmin = new AdminProfile("admin-deleted", AdminRoleType.Admin);
        deletedAdmin.SoftDelete(superAdminId, DateTime.UtcNow);

        db.AdminProfiles.AddRange(activeSuper, activeAdmin, deletedAdmin);
        await db.SaveChangesAsync();

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(superAdminId);

        var identityService = Substitute.For<IIdentityService>();
        identityService.GetUserDetailsByIdsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, (string Email, string? PhoneNumber)>
            {
                [superAdminId] = ("super1@cebizpay.com", null),
                ["admin-active"] = ("active@cebizpay.com", null)
            });

        var handler = new GetAdminDirectoryQueryHandler(db, currentUserService, identityService);
        var query = new GetAdminDirectoryQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.DoesNotContain(result.Items, i => i.UserId == "admin-deleted");
    }
}
