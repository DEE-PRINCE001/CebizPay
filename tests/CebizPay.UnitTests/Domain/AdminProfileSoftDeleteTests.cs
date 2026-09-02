using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using Xunit;

namespace CebizPay.UnitTests.Domain;

/// <summary>
/// Domain unit tests for AdminProfile soft delete, status transitions, and permission restrictions on soft-deleted profiles.
/// </summary>
public sealed class AdminProfileSoftDeleteTests
{
    [Fact]
    public void SoftDelete_ShouldSetIsDeletedTrueAndIsActiveFalse()
    {
        var profile = new AdminProfile("admin-user-1", AdminRoleType.Admin);
        profile.GrantPermission(Permissions.KycReview);
        var now = DateTime.UtcNow;

        profile.SoftDelete("super-admin-user", now);

        Assert.True(profile.IsDeleted);
        Assert.False(profile.IsActive);
        Assert.Equal("super-admin-user", profile.DeletedByUserId);
        Assert.Equal(now, profile.DeletedAtUtc);
    }

    [Fact]
    public void SoftDelete_SuperAdmin_ShouldDenyAllPermissions()
    {
        var superAdmin = new AdminProfile("super-1", AdminRoleType.SuperAdmin);
        Assert.True(superAdmin.HasPermission(Permissions.KycReview));

        superAdmin.SoftDelete("super-2", DateTime.UtcNow);

        Assert.False(superAdmin.HasPermission(Permissions.KycReview));
        Assert.False(superAdmin.HasPermission(Permissions.AdminsManagePermissions));
    }

    [Fact]
    public void Activate_OnSoftDeletedProfile_ShouldThrow()
    {
        var profile = new AdminProfile("admin-user-2", AdminRoleType.Admin);
        profile.SoftDelete("super-admin", DateTime.UtcNow);

        var ex = Assert.Throws<InvalidOperationException>(() => profile.Activate());
        Assert.Contains("Cannot activate a soft-deleted", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SoftDelete_AlreadyDeletedProfile_ShouldBeIdempotent()
    {
        var profile = new AdminProfile("admin-user-3", AdminRoleType.Admin);
        var initialTime = DateTime.UtcNow.AddMinutes(-5);
        profile.SoftDelete("super-admin-1", initialTime);

        profile.SoftDelete("super-admin-2", DateTime.UtcNow);

        Assert.Equal(initialTime, profile.DeletedAtUtc);
        Assert.Equal("super-admin-1", profile.DeletedByUserId);
    }
}
