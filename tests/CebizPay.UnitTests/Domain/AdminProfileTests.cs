using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using Xunit;

namespace CebizPay.UnitTests.Domain;

/// <summary>
/// Unit tests for AdminProfile domain entity covering RBAC rules, permissions, and MFA state.
/// </summary>
public sealed class AdminProfileTests
{
    // ─── Creation ────────────────────────────────────────────────────────────

    [Fact]
    public void CreateSuperAdmin_ShouldSetRoleAndBeActiveWithNoDefaultPermissions()
    {
        var profile = new AdminProfile("user-1", AdminRoleType.SuperAdmin);

        Assert.NotEqual(Guid.Empty, profile.Id);
        Assert.Equal("user-1", profile.UserId);
        Assert.Equal(AdminRoleType.SuperAdmin, profile.Role);
        Assert.True(profile.IsActive);
        Assert.False(profile.IsMfaEnabled);
        // SuperAdmin gets no explicit permission list; HasPermission returns true for any permission
        Assert.True(profile.HasPermission(Permissions.KycReview));
        Assert.True(profile.HasPermission(Permissions.AdminsManagePermissions));
    }

    [Fact]
    public void CreateAuditor_ShouldAutoAssignReadOnlyPermissions()
    {
        var profile = new AdminProfile("user-2", AdminRoleType.Auditor);

        Assert.Equal(AdminRoleType.Auditor, profile.Role);

        foreach (var p in Permissions.ReadOnlyAdminPermissions)
        {
            Assert.True(profile.HasPermission(p), $"Auditor should have read-only permission: {p}");
        }

        // Auditor must NOT have write permissions
        Assert.False(profile.HasPermission(Permissions.KycReview));
        Assert.False(profile.HasPermission(Permissions.AdminsManagePermissions));
        Assert.False(profile.HasPermission(Permissions.OrganizationsSuspend));
    }

    [Fact]
    public void CreateAdmin_ShouldStartWithNoPermissions()
    {
        var profile = new AdminProfile("user-3", AdminRoleType.Admin);

        Assert.Equal(AdminRoleType.Admin, profile.Role);
        Assert.False(profile.HasPermission(Permissions.KycReview));
        Assert.False(profile.HasPermission(Permissions.KybReview));
    }

    [Fact]
    public void CreateAdminProfile_WithEmptyUserId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new AdminProfile(string.Empty, AdminRoleType.Admin));
        Assert.Throws<ArgumentException>(() => new AdminProfile("   ", AdminRoleType.Admin));
    }

    // ─── Permission Delegation ─────────────────────────────────────────────

    [Fact]
    public void GrantPermission_ToAdmin_ShouldAddPermissionToList()
    {
        var profile = new AdminProfile("user-4", AdminRoleType.Admin);

        profile.GrantPermission(Permissions.KycReview);

        Assert.True(profile.HasPermission(Permissions.KycReview));
    }

    [Fact]
    public void GrantPermission_DuplicatePermission_ShouldNotDuplicate()
    {
        var profile = new AdminProfile("user-5", AdminRoleType.Admin);

        profile.GrantPermission(Permissions.KycReview);
        profile.GrantPermission(Permissions.KycReview);

        Assert.Single(profile.PermissionsList, Permissions.KycReview);
    }

    [Fact]
    public void RevokePermission_ShouldRemovePermissionFromList()
    {
        var profile = new AdminProfile("user-6", AdminRoleType.Admin);
        profile.GrantPermission(Permissions.KycReview);

        profile.RevokePermission(Permissions.KycReview);

        Assert.False(profile.HasPermission(Permissions.KycReview));
    }

    [Fact]
    public void RevokePermission_NotPresent_ShouldNotThrow()
    {
        var profile = new AdminProfile("user-7", AdminRoleType.Admin);

        // Should not throw
        profile.RevokePermission(Permissions.KycReview);
        Assert.False(profile.HasPermission(Permissions.KycReview));
    }

    [Fact]
    public void GrantPermission_EmptyPermission_ShouldThrow()
    {
        var profile = new AdminProfile("user-8", AdminRoleType.Admin);

        Assert.Throws<ArgumentException>(() => profile.GrantPermission(string.Empty));
    }

    // ─── SuperAdmin HasPermission Always Returns True ─────────────────────

    [Theory]
    [InlineData(Permissions.KycReview)]
    [InlineData(Permissions.KybReview)]
    [InlineData(Permissions.AdminsManagePermissions)]
    [InlineData(Permissions.OrganizationsSuspend)]
    [InlineData(Permissions.PayrollExecute)]
    public void SuperAdmin_HasPermission_ShouldReturnTrueForAnyPermission(string permission)
    {
        var profile = new AdminProfile("super-1", AdminRoleType.SuperAdmin);

        Assert.True(profile.HasPermission(permission));
    }

    // ─── Admin WITHOUT Permission Is Denied ───────────────────────────────

    [Theory]
    [InlineData(Permissions.KycReview)]
    [InlineData(Permissions.KybReview)]
    [InlineData(Permissions.AdminsManagePermissions)]
    public void Admin_WithoutDelegatedPermission_ShouldBeDenied(string permission)
    {
        var profile = new AdminProfile("admin-1", AdminRoleType.Admin);

        Assert.False(profile.HasPermission(permission));
    }

    // ─── Admin WITH Delegated Permission Is Allowed ───────────────────────

    [Fact]
    public void Admin_WithDelegatedKycReview_ShouldBeAllowed()
    {
        var profile = new AdminProfile("admin-2", AdminRoleType.Admin);
        profile.GrantPermission(Permissions.KycReview);

        Assert.True(profile.HasPermission(Permissions.KycReview));
        // But NOT KybReview
        Assert.False(profile.HasPermission(Permissions.KybReview));
    }

    // ─── MFA State ────────────────────────────────────────────────────────

    [Fact]
    public void SetMfaStatus_Enable_ShouldSetMfaEnabledTrue()
    {
        var profile = new AdminProfile("user-9", AdminRoleType.Admin);
        Assert.False(profile.IsMfaEnabled);

        profile.SetMfaStatus(true);

        Assert.True(profile.IsMfaEnabled);
    }

    [Fact]
    public void SetMfaStatus_Disable_ShouldSetMfaEnabledFalse()
    {
        var profile = new AdminProfile("user-10", AdminRoleType.Admin, isMfaEnabled: true);

        profile.SetMfaStatus(false);

        Assert.False(profile.IsMfaEnabled);
    }

    // ─── Activate / Deactivate ────────────────────────────────────────────

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var profile = new AdminProfile("user-11", AdminRoleType.Admin);

        profile.Deactivate();

        Assert.False(profile.IsActive);
    }

    [Fact]
    public void Activate_ShouldSetIsActiveTrue()
    {
        var profile = new AdminProfile("user-12", AdminRoleType.Admin);
        profile.Deactivate();

        profile.Activate();

        Assert.True(profile.IsActive);
    }

    // ─── Role Change ──────────────────────────────────────────────────────

    [Fact]
    public void ChangeRole_ToAuditor_ShouldReplacePermissionsWithReadOnlySet()
    {
        var profile = new AdminProfile("user-13", AdminRoleType.Admin);
        profile.GrantPermission(Permissions.KycReview);

        profile.ChangeRole(AdminRoleType.Auditor);

        Assert.Equal(AdminRoleType.Auditor, profile.Role);
        // Should now have read-only set only
        foreach (var p in Permissions.ReadOnlyAdminPermissions)
        {
            Assert.True(profile.HasPermission(p));
        }
        // KycReview (write) is not in read-only set
        Assert.False(profile.HasPermission(Permissions.KycReview));
    }
}
