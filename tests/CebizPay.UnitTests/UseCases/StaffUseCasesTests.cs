using CebizPay.Application.Common.Interfaces.Loans;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Organizations.Staff;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.UseCases;

public sealed class StaffUseCasesTests
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
    public async Task GetStaffDirectoryQuery_ReturnsPagedStaffDirectoryWithDetails()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var identityService = Substitute.For<IIdentityService>();

        var org = new Organization("Apex Inc", "hr@apex.com", "+2348000000001");
        var dept = new Department(org.Id, "Engineering", null);
        var role = new WorkforceRole(org.Id, "Software Engineer", dept.Id, null);
        var level = new SalaryLevel(org.Id, "Mid-level", 500000m, "NGN");

        var profile = new IndividualProfile("usr_alice", "Alice", "Wonderland");
        var membership = new OrganizationMembership("usr_alice", org.Id, MembershipRoleType.Member, dept.Id, role.Id, level.Id);

        dbContext.Organizations.Add(org);
        dbContext.Departments.Add(dept);
        dbContext.WorkforceRoles.Add(role);
        dbContext.SalaryLevels.Add(level);
        dbContext.IndividualProfiles.Add(profile);
        dbContext.OrganizationMemberships.Add(membership);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        identityService.GetUserDetailsByIdsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, (string Email, string? PhoneNumber)>
            {
                ["usr_alice"] = ("alice@apex.com", "+2348011112222")
            });

        var handler = new GetStaffDirectoryQueryHandler(dbContext, orgContext, identityService);
        var result = await handler.Handle(new GetStaffDirectoryQuery(org.Id, null, null, null, null, null, 1, 20), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        var staffDto = result.Items[0];
        Assert.Equal("Alice", staffDto.FirstName);
        Assert.Equal("Wonderland", staffDto.LastName);
        Assert.Equal("alice@apex.com", staffDto.Email);
        Assert.Equal("Engineering", staffDto.DepartmentName);
        Assert.Equal("Software Engineer", staffDto.RoleTitle);
        Assert.Equal(500000m, staffDto.BaseSalary);
    }

    [Fact]
    public async Task AssignStaffWorkforceCommand_AssignsDetailsAndWritesOutbox()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Apex Inc", "hr@apex.com", "+2348000000001");
        var dept = new Department(org.Id, "Product", null);
        var role = new WorkforceRole(org.Id, "Product Manager", dept.Id, null);
        var level = new SalaryLevel(org.Id, "Senior", 800000m, "NGN");
        var membership = new OrganizationMembership("usr_bob", org.Id, MembershipRoleType.Member);

        dbContext.Organizations.Add(org);
        dbContext.Departments.Add(dept);
        dbContext.WorkforceRoles.Add(role);
        dbContext.SalaryLevels.Add(level);
        dbContext.OrganizationMemberships.Add(membership);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("usr_admin");

        var handler = new AssignStaffWorkforceCommandHandler(dbContext, orgContext, userContext, outbox);
        var success = await handler.Handle(
            new AssignStaffWorkforceCommand(org.Id, membership.Id, dept.Id, role.Id, level.Id),
            CancellationToken.None);

        Assert.True(success);
        var updated = await dbContext.OrganizationMemberships.FindAsync(membership.Id);
        Assert.NotNull(updated);
        Assert.Equal(dept.Id, updated.DepartmentId);
        Assert.Equal(role.Id, updated.WorkforceRoleId);
        Assert.Equal(level.Id, updated.SalaryLevelId);
        outbox.Received(1).Write(Arg.Any<StaffAssignedDomainEvent>());
    }

    [Fact]
    public async Task ReactivateStaffMembershipCommand_ReactivatesAndRestoresProfessionalStatus()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Apex Inc", "hr@apex.com", "+2348000000001");
        var profile = new IndividualProfile("usr_carol", "Carol", "Danvers");
        profile.UpdateProfessionalStatus(ProfessionalStatus.NotAStaff);

        var membership = new OrganizationMembership("usr_carol", org.Id, MembershipRoleType.Member);
        membership.SuspendWorkAccess("Temporary leave");

        dbContext.Organizations.Add(org);
        dbContext.IndividualProfiles.Add(profile);
        dbContext.OrganizationMemberships.Add(membership);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("usr_admin");

        var handler = new ReactivateStaffMembershipCommandHandler(dbContext, orgContext, userContext, outbox);
        var success = await handler.Handle(new ReactivateStaffMembershipCommand(org.Id, membership.Id), CancellationToken.None);

        Assert.True(success);
        var updatedMembership = await dbContext.OrganizationMemberships.FindAsync(membership.Id);
        Assert.NotNull(updatedMembership);
        Assert.Equal(MembershipStatus.Active, updatedMembership.Status);

        var updatedProfile = await dbContext.IndividualProfiles.FirstAsync(p => p.UserId == "usr_carol");
        Assert.Equal(ProfessionalStatus.Staff, updatedProfile.ProfessionalStatus);

        outbox.Received(1).Write(Arg.Any<StaffMembershipReactivatedDomainEvent>());
    }

    [Fact]
    public async Task TerminateStaffMembershipCommand_OffboardsStaffAndTriggersCorporateLoanConversion()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var loanService = Substitute.For<ILoanContractService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Apex Inc", "hr@apex.com", "+2348000000001");
        var profile = new IndividualProfile("usr_david", "David", "Miller");
        profile.UpdateProfessionalStatus(ProfessionalStatus.Staff);

        var membership = new OrganizationMembership("usr_david", org.Id, MembershipRoleType.Member);

        dbContext.Organizations.Add(org);
        dbContext.IndividualProfiles.Add(profile);
        dbContext.OrganizationMemberships.Add(membership);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("usr_admin");

        var handler = new TerminateStaffMembershipCommandHandler(dbContext, orgContext, userContext, loanService, outbox);
        var success = await handler.Handle(
            new TerminateStaffMembershipCommand(org.Id, membership.Id, "Contract termination"),
            CancellationToken.None);

        Assert.True(success);
        var updatedMembership = await dbContext.OrganizationMemberships.FindAsync(membership.Id);
        Assert.NotNull(updatedMembership);
        Assert.Equal(MembershipStatus.Terminated, updatedMembership.Status);
        Assert.Equal("Contract termination", updatedMembership.SuspensionReason);

        // Verify corporate loan conversion was invoked
        await loanService.Received(1).ConvertTerminatedStaffLoansAsync(
            org.Id, "usr_david", "Contract termination", "usr_admin", Arg.Any<CancellationToken>());

        var updatedProfile = await dbContext.IndividualProfiles.FirstAsync(p => p.UserId == "usr_david");
        Assert.Equal(ProfessionalStatus.NotAStaff, updatedProfile.ProfessionalStatus);

        outbox.Received(1).Write(Arg.Any<StaffMembershipTerminatedDomainEvent>());
    }

    [Fact]
    public async Task CreateStaffDirectCommand_CreatesUserAndMembershipDirectly()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var identityService = Substitute.For<IIdentityService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Apex Inc", "hr@apex.com", "+2348000000001");
        var dept = new Department(org.Id, "HR", null);
        dbContext.Organizations.Add(org);
        dbContext.Departments.Add(dept);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("usr_admin");

        identityService.FindUserByEmailAsync("newhire@apex.com", Arg.Any<CancellationToken>())
            .Returns((false, string.Empty, string.Empty, null));

        identityService.RegisterUserAsync("newhire@apex.com", Arg.Any<string>(), "+2348099998888", Arg.Any<CancellationToken>())
            .Returns((true, "usr_newhire", Array.Empty<string>()));

        var handler = new CreateStaffDirectCommandHandler(dbContext, orgContext, userContext, identityService, outbox);
        var membershipId = await handler.Handle(
            new CreateStaffDirectCommand(org.Id, "newhire@apex.com", "Emily", "Watson", "+2348099998888", dept.Id),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, membershipId);
        var membership = await dbContext.OrganizationMemberships.FindAsync(membershipId);
        Assert.NotNull(membership);
        Assert.Equal("usr_newhire", membership.UserId);
        Assert.Equal(dept.Id, membership.DepartmentId);
        Assert.Equal(MembershipStatus.Active, membership.Status);

        var profile = await dbContext.IndividualProfiles.FirstOrDefaultAsync(p => p.UserId == "usr_newhire");
        Assert.NotNull(profile);
        Assert.Equal("Emily", profile.FirstName);
        Assert.Equal(ProfessionalStatus.Staff, profile.ProfessionalStatus);

        outbox.Received(1).Write(Arg.Any<StaffDirectCreatedDomainEvent>());
    }

    [Fact]
    public async Task InviteStaffBulkCommand_ProcessesBatchAndGeneratesInvitations()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var identityService = Substitute.For<IIdentityService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Apex Inc", "hr@apex.com", "+2348000000001");
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("usr_admin");

        identityService.FindUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((false, string.Empty, string.Empty, null));

        var handler = new InviteStaffBulkCommandHandler(dbContext, orgContext, userContext, identityService, outbox);
        var emails = new List<string> { "staff1@apex.com", "staff2@apex.com", "staff1@apex.com" }; // duplicate email included

        var summary = await handler.Handle(new InviteStaffBulkCommand(org.Id, emails), CancellationToken.None);

        Assert.Equal(2, summary.TotalRequested); // deduplicated
        Assert.Equal(2, summary.TotalSuccess);
        Assert.Equal(0, summary.TotalFailed);

        var invitations = await dbContext.StaffInvitations.Where(i => i.OrganizationId == org.Id).ToListAsync();
        Assert.Equal(2, invitations.Count);

        outbox.Received(2).Write(Arg.Any<StaffInvitationCreatedDomainEvent>());
        outbox.Received(1).Write(Arg.Any<StaffBulkInvitationsCreatedDomainEvent>());
    }
}
