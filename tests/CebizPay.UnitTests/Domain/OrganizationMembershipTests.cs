using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using Xunit;

namespace CebizPay.UnitTests.Domain;

public sealed class OrganizationMembershipTests
{
    [Fact]
    public void SuspendWorkAccess_ShouldBlockWorkplaceAccess_WithoutAffectingPersonalProfile()
    {
        // Arrange
        var userId = "user-456";
        var orgId = Guid.NewGuid();
        var profile = new IndividualProfile(userId, "Jane", "Smith");
        profile.SetKycStatus(KycStatus.Verified);
        profile.UpdateProfessionalStatus(ProfessionalStatus.Staff);

        var membership = new OrganizationMembership(userId, orgId, MembershipRoleType.Member);

        // Act - Suspend work relationship for this organization
        membership.SuspendWorkAccess("Workplace policy violation");

        // Assert - Work membership is suspended
        Assert.Equal(MembershipStatus.Suspended, membership.Status);
        Assert.False(membership.IsActiveWorkplaceMember());
        Assert.NotNull(membership.SuspendedAtUtc);
        Assert.Equal("Workplace policy violation", membership.SuspensionReason);

        // CRITICAL PRD REQUIREMENT: Individual identity & KYC remain intact!
        Assert.Equal(KycStatus.Verified, profile.KycStatus);
        Assert.False(profile.IsSubjectToTransactionCap());
        Assert.True(profile.CanAcceptStaffInvitation());
    }
}
