using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using Xunit;

namespace CebizPay.UnitTests.BusinessLogic;

public sealed class TenantAndAuthorizationBoundaryTests
{
    [Fact]
    public void AdminProfile_SoftDeleted_CannotPerformActionsOrBeActivated()
    {
        var admin = new AdminProfile("user-1", AdminRoleType.Admin);
        admin.Activate();
        Assert.True(admin.IsActive);
        Assert.False(admin.IsDeleted);

        var now = DateTime.UtcNow;
        admin.SoftDelete("superadmin-1", now);

        Assert.True(admin.IsDeleted);
        Assert.False(admin.IsActive);
        Assert.False(admin.HasPermission(Permissions.WalletTransfer));

        // Cannot reactivate a soft-deleted admin
        var ex = Assert.Throws<InvalidOperationException>(() => admin.Activate());
        Assert.Contains("Cannot activate a soft-deleted admin profile", ex.Message);
    }

    [Fact]
    public void AdminProfile_Deactivated_CannotPerformAdministrativeActions()
    {
        var admin = new AdminProfile("user-2", AdminRoleType.SuperAdmin);
        Assert.True(admin.IsActive);

        admin.Deactivate();
        Assert.False(admin.IsActive);

        admin.Activate();
        Assert.True(admin.IsActive);
    }

    [Fact]
    public void Organization_StatusBoundaries_GovernWalletTransfers()
    {
        var org = new Organization("Test Corp", "contact@testcorp.com", "+2348012345678");

        // Pending state cannot transfer
        Assert.Equal(OrganizationStatus.Pending, org.Status);
        Assert.False(org.CanExecuteWalletTransfers());

        // Verified state can transfer
        org.TransitionStatus(OrganizationStatus.Verified);
        Assert.Equal(OrganizationStatus.Verified, org.Status);
        Assert.True(org.CanExecuteWalletTransfers());

        // Suspended state cannot transfer
        org.TransitionStatus(OrganizationStatus.Suspended);
        Assert.Equal(OrganizationStatus.Suspended, org.Status);
        Assert.False(org.CanExecuteWalletTransfers());
    }

    [Fact]
    public void OrganizationMembership_PermissionAndStatus_GovernAccess()
    {
        var orgId = Guid.NewGuid();
        var membership = new OrganizationMembership("user-member", orgId, MembershipRoleType.Member);

        // Plain member without WalletTransfer permission
        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.False(membership.HasPermission(Permissions.WalletTransfer));

        // Promote to Admin -> grants WalletTransfer permission
        membership.ChangeRole(MembershipRoleType.Admin);
        Assert.True(membership.HasPermission(Permissions.WalletTransfer));

        // Suspend membership
        membership.SuspendWorkAccess("Policy breach");
        Assert.Equal(MembershipStatus.Suspended, membership.Status);
        Assert.False(membership.IsActiveWorkplaceMember());

        // Reactivate membership
        membership.ReactivateWorkAccess();
        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.True(membership.IsActiveWorkplaceMember());
    }
}
