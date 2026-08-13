using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using Xunit;

namespace CebizPay.UnitTests.Domain;

public sealed class OrganizationTests
{
    [Fact]
    public void CreateOrganization_ShouldInitializeInPendingStatusAndStep1Completed()
    {
        // Act
        var org = new Organization("CebizPay Ltd", "info@cebizpay.com", "+2348000000000");

        // Assert
        Assert.NotEqual(Guid.Empty, org.Id);
        Assert.Equal("CebizPay Ltd", org.CompanyName);
        Assert.Equal("info@cebizpay.com", org.Email);
        Assert.Equal(OrganizationStatus.Pending, org.Status);
        Assert.Equal(KybStatus.Step1Completed, org.KybStatus);
        Assert.True(org.CanEditDetails);
        Assert.True(org.CanConfigureHris());
        Assert.False(org.CanExecutePayroll());
        Assert.False(org.CanExecuteWalletTransfers());
    }

    [Fact]
    public void CompleteStep2_ShouldUpdateDetailsAndKybStatus()
    {
        // Arrange
        var org = new Organization("CebizPay Ltd", "info@cebizpay.com", "+2348000000000");

        // Act
        org.CompleteStep2("RC123456", "https://logo.url", "https://cac.url");

        // Assert
        Assert.Equal("RC123456", org.CacNumber);
        Assert.Equal("https://logo.url", org.LogoUrl);
        Assert.Equal("https://cac.url", org.CacCertificateUrl);
        Assert.Equal(KybStatus.Step2Completed, org.KybStatus);
    }

    [Theory]
    [InlineData(OrganizationStatus.Verified, KybStatus.Verified, false)]
    [InlineData(OrganizationStatus.Rejected, KybStatus.Rejected, true)]
    public void TransitionStatus_ValidTransitions_ShouldUpdateStatusAndEditPermissions(
        OrganizationStatus targetStatus, KybStatus expectedKybStatus, bool expectedCanEdit)
    {
        // Arrange
        var org = new Organization("CebizPay Ltd", "info@cebizpay.com", "+2348000000000");

        // Act
        org.TransitionStatus(targetStatus, "Admin review");

        // Assert
        Assert.Equal(targetStatus, org.Status);
        Assert.Equal(expectedKybStatus, org.KybStatus);
        Assert.Equal(expectedCanEdit, org.CanEditDetails);
    }

    [Fact]
    public void TransitionStatus_VerifiedToSuspendedToVerified_ShouldSucceed()
    {
        // Arrange
        var org = new Organization("CebizPay Ltd", "info@cebizpay.com", "+2348000000000");
        org.TransitionStatus(OrganizationStatus.Verified);

        // Act & Assert (Verified -> Suspended)
        org.TransitionStatus(OrganizationStatus.Suspended, "Compliance review");
        Assert.Equal(OrganizationStatus.Suspended, org.Status);
        Assert.Equal(KybStatus.Suspended, org.KybStatus);
        Assert.False(org.CanConfigureHris());
        Assert.False(org.CanExecutePayroll());

        // Act & Assert (Suspended -> Verified)
        org.TransitionStatus(OrganizationStatus.Verified, "Reactivated");
        Assert.Equal(OrganizationStatus.Verified, org.Status);
        Assert.True(org.CanExecutePayroll());
        Assert.True(org.CanExecuteWalletTransfers());
    }

    [Fact]
    public void TransitionStatus_InvalidTransition_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var org = new Organization("CebizPay Ltd", "info@cebizpay.com", "+2348000000000");

        // Act & Assert: Pending -> Suspended directly is invalid
        Assert.Throws<InvalidOperationException>(() => org.TransitionStatus(OrganizationStatus.Suspended));
    }
}
