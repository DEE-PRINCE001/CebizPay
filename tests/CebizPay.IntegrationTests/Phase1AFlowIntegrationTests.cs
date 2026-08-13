using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.IntegrationTests;

public sealed class Phase1AFlowIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public Phase1AFlowIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CompletePhase1A_LifecycleFlow_ShouldExecuteSuccessfully()
    {
        // Arrange
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        // 1. Create Individual Profile & Verify KYC
        var userId = $"indiv_{Guid.NewGuid():N}";
        var profile = new IndividualProfile(userId, "Alice", "Johnson");
        dbContext.IndividualProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        Assert.Equal(KycStatus.Pending, profile.KycStatus);
        Assert.False(profile.CanAcceptStaffInvitation());

        profile.SetKycStatus(KycStatus.Verified);
        await dbContext.SaveChangesAsync();

        Assert.Equal(KycStatus.Verified, profile.KycStatus);
        Assert.True(profile.CanAcceptStaffInvitation());

        // 2. Organization Step 1 & Step 2 Registration
        var org = new Organization("Acme Payroll Ltd", $"acme_{Guid.NewGuid():N}@payroll.com", "+2348111111111");
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync();

        org.CompleteStep2("RC987654", "https://acme.com/logo.png", "https://acme.com/cac.pdf");
        org.TransitionStatus(OrganizationStatus.Verified, "Super Admin Approved");
        await dbContext.SaveChangesAsync();

        Assert.Equal(OrganizationStatus.Verified, org.Status);
        Assert.Equal(KybStatus.Verified, org.KybStatus);
        Assert.True(org.CanExecutePayroll());

        // 3. Staff Invitation & Acceptance
        var invitation = new StaffInvitation(org.Id, "alice@payroll.com");
        dbContext.StaffInvitations.Add(invitation);
        await dbContext.SaveChangesAsync();

        invitation.Accept(DateTime.UtcNow);
        var membership = new OrganizationMembership(userId, org.Id, MembershipRoleType.Member);
        dbContext.OrganizationMemberships.Add(membership);
        profile.UpdateProfessionalStatus(ProfessionalStatus.Staff);
        await dbContext.SaveChangesAsync();

        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.Equal(ProfessionalStatus.Staff, profile.ProfessionalStatus);

        // 4. Staff Suspension
        membership.SuspendWorkAccess("Violation of corporate remote work policy");
        profile.UpdateProfessionalStatus(ProfessionalStatus.NotAStaff);
        await dbContext.SaveChangesAsync();

        // Assert: Workplace access suspended
        Assert.Equal(MembershipStatus.Suspended, membership.Status);
        Assert.Equal("Violation of corporate remote work policy", membership.SuspensionReason);

        // Assert: Personal KYC & global identity REMAIN VERIFIED AND UNTOUCHED
        Assert.Equal(KycStatus.Verified, profile.KycStatus);
        Assert.False(profile.IsSubjectToTransactionCap());
    }
}
