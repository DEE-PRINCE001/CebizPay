using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using Xunit;

namespace CebizPay.UnitTests.Domain;

public sealed class IndividualProfileTests
{
    [Fact]
    public void CreateProfile_ShouldInitializeInPendingKycAndNotAStaff()
    {
        // Act
        var profile = new IndividualProfile("user-123", "John", "Doe");

        // Assert
        Assert.Equal("user-123", profile.UserId);
        Assert.Equal("John", profile.FirstName);
        Assert.Equal("Doe", profile.LastName);
        Assert.Equal(KycStatus.Pending, profile.KycStatus);
        Assert.Equal(ProfessionalStatus.NotAStaff, profile.ProfessionalStatus);
        Assert.True(profile.IsSubjectToTransactionCap());
        Assert.False(profile.CanAcceptStaffInvitation());
    }

    [Fact]
    public void SetKycStatus_Verified_ShouldEnableStaffInvitationAcceptanceAndClearTransactionCap()
    {
        // Arrange
        var profile = new IndividualProfile("user-123", "John", "Doe");

        // Act
        profile.SetKycStatus(KycStatus.Verified);

        // Assert
        Assert.Equal(KycStatus.Verified, profile.KycStatus);
        Assert.False(profile.IsSubjectToTransactionCap());
        Assert.True(profile.CanAcceptStaffInvitation());
    }

    [Fact]
    public void SetKycStatus_InvalidTransition_FromVerifiedToPending_ShouldThrowException()
    {
        // Arrange
        var profile = new IndividualProfile("user-123", "John", "Doe");
        profile.SetKycStatus(KycStatus.Verified);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => profile.SetKycStatus(KycStatus.Pending));
    }
}
