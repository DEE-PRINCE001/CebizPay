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

    [Fact]
    public void Suspend_WhenActive_ShouldSetIsSuspendedAndReason()
    {
        // Arrange
        var profile = new IndividualProfile("user-123", "John", "Doe");

        // Act
        profile.Suspend("Compliance investigation under CBN CDD regulations");

        // Assert
        Assert.True(profile.IsSuspended);
        Assert.Equal("Compliance investigation under CBN CDD regulations", profile.SuspensionReason);
        Assert.NotNull(profile.SuspendedAtUtc);
        Assert.False(profile.CanTransactOutbound());
    }

    [Fact]
    public void Suspend_WhenAlreadySuspended_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var profile = new IndividualProfile("user-123", "John", "Doe");
        profile.Suspend("Initial reason");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => profile.Suspend("Second reason"));
        Assert.Equal("Individual profile is already suspended.", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Suspend_WithInvalidReason_ShouldThrowArgumentException(string? invalidReason)
    {
        // Arrange
        var profile = new IndividualProfile("user-123", "John", "Doe");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => profile.Suspend(invalidReason!));
    }

    [Fact]
    public void Reactivate_WhenSuspended_ShouldClearSuspensionState()
    {
        // Arrange
        var profile = new IndividualProfile("user-123", "John", "Doe");
        profile.Suspend("Temporary regulatory review");

        // Act
        profile.Reactivate();

        // Assert
        Assert.False(profile.IsSuspended);
        Assert.Null(profile.SuspensionReason);
        Assert.Null(profile.SuspendedAtUtc);
        Assert.True(profile.CanTransactOutbound());
    }

    [Fact]
    public void Reactivate_WhenNotSuspended_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var profile = new IndividualProfile("user-123", "John", "Doe");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => profile.Reactivate());
        Assert.Equal("Individual profile is not suspended.", ex.Message);
    }
}
