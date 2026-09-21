using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.UseCases.Organizations.Workforce;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.UseCases;

public sealed class CompositeWorkforceUseCasesTests
{
    private static readonly string[] SampleRoles = ["UI/UX Intern", "Entry Level Designer", "Lead Product Designer"];
    private static readonly string[] SingleSecurityRole = ["Guard"];

    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateDepartmentWithRoles_WhenValid_PersistsDepartmentAndRolesAtomically()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Acme Corp", "admin@acme.com", "+2348000000001");
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("usr_admin");

        var handler = new CreateDepartmentWithRolesCommandHandler(dbContext, orgContext, userContext, outbox);
        var command = new CreateDepartmentWithRolesCommand(
            org.Id,
            "Product & Design",
            "Product design and UX research",
            SampleRoles);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(org.Id, result.OrganizationId);
        Assert.Equal("Product & Design", result.Name);
        Assert.Equal("Product design and UX research", result.Description);
        Assert.Equal(3, result.Roles.Count);

        var savedDept = await dbContext.Departments.FindAsync(result.Id);
        Assert.NotNull(savedDept);

        var savedRoles = await dbContext.WorkforceRoles.Where(r => r.DepartmentId == result.Id).ToListAsync();
        Assert.Equal(3, savedRoles.Count);
        Assert.Contains(savedRoles, r => r.Title == "UI/UX Intern");
        Assert.Contains(savedRoles, r => r.Title == "Entry Level Designer");
        Assert.Contains(savedRoles, r => r.Title == "Lead Product Designer");

        outbox.Received(1).Write(Arg.Any<DepartmentCreatedDomainEvent>());
        outbox.Received(3).Write(Arg.Any<WorkforceRoleCreatedDomainEvent>());
    }

    [Fact]
    public async Task CreateDepartmentWithRoles_WhenNoRolesProvided_CreatesDepartmentWithEmptyRolesList()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Acme Corp", "admin@acme.com", "+2348000000001");
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("usr_admin");

        var handler = new CreateDepartmentWithRolesCommandHandler(dbContext, orgContext, userContext, outbox);
        var command = new CreateDepartmentWithRolesCommand(org.Id, "Logistics", null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Logistics", result.Name);
        Assert.Empty(result.Roles);

        var savedDept = await dbContext.Departments.FindAsync(result.Id);
        Assert.NotNull(savedDept);
        outbox.Received(1).Write(Arg.Any<DepartmentCreatedDomainEvent>());
        outbox.DidNotReceive().Write(Arg.Any<WorkforceRoleCreatedDomainEvent>());
    }

    [Fact]
    public async Task CreateDepartmentWithRoles_WhenDuplicateName_ThrowsInvalidOperationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Acme Corp", "admin@acme.com", "+2348000000001");
        var existingDept = new Department(org.Id, "Security", null);
        dbContext.Organizations.Add(org);
        dbContext.Departments.Add(existingDept);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);

        var handler = new CreateDepartmentWithRolesCommandHandler(dbContext, orgContext, userContext, outbox);
        var command = new CreateDepartmentWithRolesCommand(org.Id, "security", null, SingleSecurityRole);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task CreateDepartmentWithRoles_WhenTenantAccessFails_ThrowsUnauthorizedAccessException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var orgId = Guid.NewGuid();
        orgContext.HasAccessToOrganizationAsync(orgId).Returns(false);

        var handler = new CreateDepartmentWithRolesCommandHandler(dbContext, orgContext, userContext, outbox);
        var command = new CreateDepartmentWithRolesCommand(orgId, "Finance", null, null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task CreateSalaryLevelWithMembers_WhenValid_PersistsLevelAndAssignsStaffMembers()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Acme Corp", "admin@acme.com", "+2348000000001");
        var staff1 = new OrganizationMembership("usr_1", org.Id, MembershipRoleType.Member);
        var staff2 = new OrganizationMembership("usr_2", org.Id, MembershipRoleType.Member);
        dbContext.Organizations.Add(org);
        dbContext.OrganizationMemberships.AddRange(staff1, staff2);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("usr_admin");

        var handler = new CreateSalaryLevelWithMembersCommandHandler(dbContext, orgContext, userContext, outbox);
        var memberIds = new List<Guid> { staff1.Id, staff2.Id };
        var command = new CreateSalaryLevelWithMembersCommand(
            org.Id,
            "Level 10",
            500000.00m,
            "NGN",
            memberIds);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(org.Id, result.OrganizationId);
        Assert.Equal("Level 10", result.LevelName);
        Assert.Equal(500000.00m, result.BaseAmount);
        Assert.Equal("NGN", result.Currency);
        Assert.Equal(2, result.AssignedStaffCount);

        var savedLevel = await dbContext.SalaryLevels.FindAsync(result.Id);
        Assert.NotNull(savedLevel);

        var updatedStaff1 = await dbContext.OrganizationMemberships.FindAsync(staff1.Id);
        var updatedStaff2 = await dbContext.OrganizationMemberships.FindAsync(staff2.Id);
        Assert.Equal(result.Id, updatedStaff1!.SalaryLevelId);
        Assert.Equal(result.Id, updatedStaff2!.SalaryLevelId);

        outbox.Received(1).Write(Arg.Any<SalaryLevelCreatedDomainEvent>());
        outbox.Received(2).Write(Arg.Any<StaffAssignedDomainEvent>());
    }

    [Fact]
    public async Task CreateSalaryLevelWithMembers_WhenNoMembers_CreatesLevelWithZeroAssigned()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Acme Corp", "admin@acme.com", "+2348000000001");
        dbContext.Organizations.Add(org);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("usr_admin");

        var handler = new CreateSalaryLevelWithMembersCommandHandler(dbContext, orgContext, userContext, outbox);
        var command = new CreateSalaryLevelWithMembersCommand(org.Id, "Level 1", 150000.00m, "NGN", null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(0, result.AssignedStaffCount);

        outbox.Received(1).Write(Arg.Any<SalaryLevelCreatedDomainEvent>());
        outbox.DidNotReceive().Write(Arg.Any<StaffAssignedDomainEvent>());
    }

    [Fact]
    public async Task CreateSalaryLevelWithMembers_WhenMemberNotFoundOrDifferentOrg_ThrowsKeyNotFoundException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Acme Corp", "admin@acme.com", "+2348000000001");
        var otherOrg = new Organization("Other Corp", "admin@other.com", "+2348000000002");
        var otherStaff = new OrganizationMembership("usr_other", otherOrg.Id, MembershipRoleType.Member);
        dbContext.Organizations.AddRange(org, otherOrg);
        dbContext.OrganizationMemberships.Add(otherStaff);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("usr_admin");

        var handler = new CreateSalaryLevelWithMembersCommandHandler(dbContext, orgContext, userContext, outbox);
        var targetIds = new List<Guid> { otherStaff.Id, Guid.NewGuid() };
        var command = new CreateSalaryLevelWithMembersCommand(
            org.Id,
            "Level 2",
            200000.00m,
            "NGN",
            targetIds);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task CreateSalaryLevelWithMembers_WhenStaffMemberTerminated_ThrowsInvalidOperationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Acme Corp", "admin@acme.com", "+2348000000001");
        var staff = new OrganizationMembership("usr_terminated", org.Id, MembershipRoleType.Member);
        staff.TerminateWorkAccess("Offboarded");
        dbContext.Organizations.Add(org);
        dbContext.OrganizationMemberships.Add(staff);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("usr_admin");

        var handler = new CreateSalaryLevelWithMembersCommandHandler(dbContext, orgContext, userContext, outbox);
        var targetIds = new List<Guid> { staff.Id };
        var command = new CreateSalaryLevelWithMembersCommand(
            org.Id,
            "Level 3",
            250000.00m,
            "NGN",
            targetIds);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task CreateSalaryLevelWithMembers_WhenDuplicateLevelName_ThrowsInvalidOperationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Acme Corp", "admin@acme.com", "+2348000000001");
        var existingLevel = new SalaryLevel(org.Id, "Level 10", 400000m, "NGN");
        dbContext.Organizations.Add(org);
        dbContext.SalaryLevels.Add(existingLevel);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);

        var handler = new CreateSalaryLevelWithMembersCommandHandler(dbContext, orgContext, userContext, outbox);
        var command = new CreateSalaryLevelWithMembersCommand(org.Id, "level 10", 500000m, "NGN", null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(command, CancellationToken.None));
    }
}
