using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using Xunit;

namespace CebizPay.UnitTests.Domain;

public sealed class WorkforceEntitiesTests
{
    [Fact]
    public void Department_Update_ShouldUpdateNameAndDescription()
    {
        var orgId = Guid.NewGuid();
        var dept = new Department(orgId, "Engineering", "Tech team");

        dept.Update("Product Engineering", "Core tech and infrastructure");

        Assert.Equal("Product Engineering", dept.Name);
        Assert.Equal("Core tech and infrastructure", dept.Description);
    }

    [Fact]
    public void WorkforceRole_Update_ShouldUpdateTitleDepartmentAndDescription()
    {
        var orgId = Guid.NewGuid();
        var deptId1 = Guid.NewGuid();
        var deptId2 = Guid.NewGuid();
        var role = new WorkforceRole(orgId, "Junior Dev", deptId1, "Entry level dev");

        role.Update("Senior Dev", deptId2, "Lead engineer");

        Assert.Equal("Senior Dev", role.Title);
        Assert.Equal(deptId2, role.DepartmentId);
        Assert.Equal("Lead engineer", role.Description);
    }

    [Fact]
    public void SalaryLevel_Update_ShouldUpdateLevelNameBaseAmountAndCurrency()
    {
        var orgId = Guid.NewGuid();
        var salaryLevel = new SalaryLevel(orgId, "L1", 250000m, "NGN");

        salaryLevel.Update("L2", 400000m, "USD");

        Assert.Equal("L2", salaryLevel.LevelName);
        Assert.Equal(400000m, salaryLevel.BaseAmount);
        Assert.Equal("USD", salaryLevel.Currency);
    }

    [Fact]
    public void OrganizationMembership_TerminateWorkAccess_ShouldSetTerminatedStatusAndReason()
    {
        var orgId = Guid.NewGuid();
        var membership = new OrganizationMembership("usr_123", orgId, MembershipRoleType.Member);

        membership.TerminateWorkAccess("Mutual agreement offboarding");

        Assert.Equal(MembershipStatus.Terminated, membership.Status);
        Assert.False(membership.IsActiveWorkplaceMember());
        Assert.NotNull(membership.SuspendedAtUtc);
        Assert.Equal("Mutual agreement offboarding", membership.SuspensionReason);
    }

    [Fact]
    public void OrganizationMembership_ReactivateWorkAccess_ShouldRestoreActiveStatus()
    {
        var orgId = Guid.NewGuid();
        var membership = new OrganizationMembership("usr_123", orgId, MembershipRoleType.Member);
        membership.TerminateWorkAccess("Temporary contract ended");

        membership.ReactivateWorkAccess();

        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.True(membership.IsActiveWorkplaceMember());
        Assert.Null(membership.SuspendedAtUtc);
        Assert.Null(membership.SuspensionReason);
    }

    [Fact]
    public void OrganizationMembership_HasPermission_ShouldEnforceRoleAndStaffPermissions()
    {
        var orgId = Guid.NewGuid();
        var adminMembership = new OrganizationMembership("usr_admin", orgId, MembershipRoleType.Admin);
        var memberMembership = new OrganizationMembership("usr_member", orgId, MembershipRoleType.Member);

        // Admin has HRIS & Staff permissions
        Assert.True(adminMembership.HasPermission(Permissions.StaffCreate));
        Assert.True(adminMembership.HasPermission(Permissions.StaffAssign));
        Assert.True(adminMembership.HasPermission(Permissions.StaffTerminate));
        Assert.True(adminMembership.HasPermission(Permissions.StaffReactivate));
        Assert.True(adminMembership.HasPermission(Permissions.DepartmentsManage));

        // Regular Member does NOT have Staff management permissions
        Assert.False(memberMembership.HasPermission(Permissions.StaffCreate));
        Assert.False(memberMembership.HasPermission(Permissions.StaffTerminate));
    }
}
