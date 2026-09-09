using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Auth.GetCurrentUser;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Security;

public sealed class GetCurrentUserQueryHandlerTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_WhenUnauthenticated_ShouldThrowUnauthorizedAccessException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        var identityService = Substitute.For<IIdentityService>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        currentUserService.UserId.Returns((string?)null);

        var handler = new GetCurrentUserQueryHandler(currentUserService, identityService, dbContext, orgContext);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new GetCurrentUserQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenUserNotFoundInIdentity_ShouldThrowKeyNotFoundException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        var identityService = Substitute.For<IIdentityService>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        currentUserService.UserId.Returns("usr_missing");
        identityService.GetUserIdentityByIdAsync("usr_missing", Arg.Any<CancellationToken>())
            .Returns((UserIdentityDetails?)null);

        var handler = new GetCurrentUserQueryHandler(currentUserService, identityService, dbContext, orgContext);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new GetCurrentUserQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenAuthenticatedWithFullProfileAndOrgs_ShouldReturnCompleteDto()
    {
        using var dbContext = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        var identityService = Substitute.For<IIdentityService>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        const string userId = "usr_123";
        currentUserService.UserId.Returns(userId);

        var identityDetails = new UserIdentityDetails(
            UserId: userId,
            Email: "john.doe@example.com",
            EmailConfirmed: true,
            PhoneNumber: "+2348012345678",
            PhoneNumberConfirmed: true,
            TwoFactorEnabled: true,
            HasTransactionPin: true,
            CreatedAtUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        identityService.GetUserIdentityByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(identityDetails);

        var individual = new IndividualProfile(userId, "John", "Doe", "Adam");
        individual.SetKycStatus(KycStatus.Verified);
        individual.UpdateProfessionalStatus(ProfessionalStatus.Staff);
        dbContext.IndividualProfiles.Add(individual);

        var org = new Organization("Acme Global Ltd", "contact@acme.com", "+2348000000001");
        var dept = new Department(org.Id, "Finance", null);
        var role = new WorkforceRole(org.Id, "Accountant", dept.Id, null);
        var level = new SalaryLevel(org.Id, "Senior", 800000m, "NGN");

        var membership = new OrganizationMembership(
            userId: userId,
            organizationId: org.Id,
            role: MembershipRoleType.PayrollManager,
            departmentId: dept.Id,
            workforceRoleId: role.Id,
            salaryLevelId: level.Id);

        dbContext.Organizations.Add(org);
        dbContext.Departments.Add(dept);
        dbContext.WorkforceRoles.Add(role);
        dbContext.SalaryLevels.Add(level);
        dbContext.OrganizationMemberships.Add(membership);
        await dbContext.SaveChangesAsync();

        orgContext.CurrentOrganizationId.Returns(org.Id);

        var handler = new GetCurrentUserQueryHandler(currentUserService, identityService, dbContext, orgContext);
        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        Assert.Equal(userId, result.UserId);
        Assert.Equal("john.doe@example.com", result.Email);
        Assert.True(result.EmailConfirmed);
        Assert.Equal("+2348012345678", result.PhoneNumber);
        Assert.True(result.PhoneNumberConfirmed);
        Assert.True(result.TwoFactorEnabled);
        Assert.True(result.HasTransactionPin);
        Assert.Equal("John", result.FirstName);
        Assert.Equal("Doe", result.LastName);
        Assert.Equal("Adam", result.MiddleName);
        Assert.Equal("John Adam Doe", result.FullName);
        Assert.Equal("Verified", result.KycStatus);
        Assert.Equal("Staff", result.ProfessionalStatus);
        Assert.False(result.IsSubjectToTransactionCap);
        Assert.True(result.CanAcceptStaffInvitation);
        Assert.Null(result.AdminProfile);
        Assert.Equal(org.Id, result.ActiveOrganizationId);

        Assert.Single(result.Organizations);
        var orgResult = result.Organizations[0];
        Assert.Equal(org.Id, orgResult.OrganizationId);
        Assert.Equal("Acme Global Ltd", orgResult.CompanyName);
        Assert.Equal("PayrollManager", orgResult.Role);
        Assert.Equal("Active", orgResult.Status);
        Assert.True(orgResult.IsActive);
        Assert.Equal("Finance", orgResult.DepartmentName);
        Assert.Equal("Accountant", orgResult.WorkforceRoleTitle);
        Assert.Equal("Senior", orgResult.SalaryLevelName);
        Assert.Contains(Permissions.PayrollExecute, orgResult.Permissions);
    }

    [Fact]
    public async Task Handle_WhenSuperAdmin_ShouldReturnAdminProfileWithAllPermissions()
    {
        using var dbContext = CreateInMemoryDbContext();
        var currentUserService = Substitute.For<ICurrentUserService>();
        var identityService = Substitute.For<IIdentityService>();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        const string adminId = "usr_superadmin";
        currentUserService.UserId.Returns(adminId);

        var identityDetails = new UserIdentityDetails(
            UserId: adminId,
            Email: "superadmin@cebizpay.com",
            EmailConfirmed: true,
            PhoneNumber: null,
            PhoneNumberConfirmed: false,
            TwoFactorEnabled: false,
            HasTransactionPin: false,
            CreatedAtUtc: DateTime.UtcNow);

        identityService.GetUserIdentityByIdAsync(adminId, Arg.Any<CancellationToken>())
            .Returns(identityDetails);

        var admin = new AdminProfile(adminId, AdminRoleType.SuperAdmin, isMfaEnabled: true);
        dbContext.AdminProfiles.Add(admin);
        await dbContext.SaveChangesAsync();

        var handler = new GetCurrentUserQueryHandler(currentUserService, identityService, dbContext, orgContext);
        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        Assert.NotNull(result.AdminProfile);
        Assert.Equal("SuperAdmin", result.AdminProfile.Role);
        Assert.True(result.AdminProfile.IsActive);
        Assert.True(result.AdminProfile.IsMfaEnabled);
        Assert.NotEmpty(result.AdminProfile.Permissions);
        Assert.Empty(result.Organizations);
        Assert.Null(result.ActiveOrganizationId);
    }
}
