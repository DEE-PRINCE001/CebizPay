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

public sealed class WorkforceUseCasesTests
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
    public async Task CreateDepartment_WhenValid_PersistsAndWritesOutboxAndAudit()
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

        var handler = new CreateDepartmentCommandHandler(dbContext, orgContext, userContext, outbox);
        var deptId = await handler.Handle(new CreateDepartmentCommand(org.Id, "Finance", "Finance and Accounting"), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, deptId);
        var dept = await dbContext.Departments.FindAsync(deptId);
        Assert.NotNull(dept);
        Assert.Equal("Finance", dept.Name);

        outbox.Received(1).Write(Arg.Any<DepartmentCreatedDomainEvent>());
        var audit = await dbContext.AuditLogs.FirstOrDefaultAsync(a => a.OrganizationId == org.Id);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task CreateDepartment_WhenDuplicateName_ThrowsInvalidOperationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Acme Corp", "admin@acme.com", "+2348000000001");
        var existingDept = new Department(org.Id, "Finance", null);
        dbContext.Organizations.Add(org);
        dbContext.Departments.Add(existingDept);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);

        var handler = new CreateDepartmentCommandHandler(dbContext, orgContext, userContext, outbox);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateDepartmentCommand(org.Id, "finance", "New Description"), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteDepartment_WhenActiveStaffAssigned_ThrowsInvalidOperationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Acme Corp", "admin@acme.com", "+2348000000001");
        var dept = new Department(org.Id, "Sales", null);
        var staff = new OrganizationMembership("usr_staff", org.Id, MembershipRoleType.Member, dept.Id);
        dbContext.Organizations.Add(org);
        dbContext.Departments.Add(dept);
        dbContext.OrganizationMemberships.Add(staff);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);

        var handler = new DeleteDepartmentCommandHandler(dbContext, orgContext, userContext, outbox);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new DeleteDepartmentCommand(dept.Id, org.Id), CancellationToken.None));
    }

    [Fact]
    public async Task GetDepartmentsQuery_ReturnsActiveStaffCountsAndFilters()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();

        var org = new Organization("Acme Corp", "admin@acme.com", "+2348000000001");
        var dept1 = new Department(org.Id, "Engineering", null);
        var dept2 = new Department(org.Id, "Marketing", null);
        var staff1 = new OrganizationMembership("usr_1", org.Id, MembershipRoleType.Member, dept1.Id);
        var staff2 = new OrganizationMembership("usr_2", org.Id, MembershipRoleType.Member, dept1.Id);
        dbContext.Organizations.Add(org);
        dbContext.Departments.AddRange(dept1, dept2);
        dbContext.OrganizationMemberships.AddRange(staff1, staff2);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);

        var handler = new GetDepartmentsQueryHandler(dbContext, orgContext);
        var result = await handler.Handle(new GetDepartmentsQuery(org.Id, null, 1, 10), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        var engDto = result.Items.First(d => d.Id == dept1.Id);
        Assert.Equal(2, engDto.ActiveStaffCount);
        var mktDto = result.Items.First(d => d.Id == dept2.Id);
        Assert.Equal(0, mktDto.ActiveStaffCount);
    }

    [Fact]
    public async Task CreateWorkforceRole_WhenValid_PersistsAndWritesOutbox()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Acme Corp", "admin@acme.com", "+2348000000001");
        var dept = new Department(org.Id, "Tech", null);
        dbContext.Organizations.Add(org);
        dbContext.Departments.Add(dept);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);
        userContext.UserId.Returns("usr_admin");

        var handler = new CreateWorkforceRoleCommandHandler(dbContext, orgContext, userContext, outbox);
        var roleId = await handler.Handle(new CreateWorkforceRoleCommand(org.Id, "DevOps Engineer", dept.Id, "Infra management"), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, roleId);
        var role = await dbContext.WorkforceRoles.FindAsync(roleId);
        Assert.NotNull(role);
        Assert.Equal("DevOps Engineer", role.Title);
        outbox.Received(1).Write(Arg.Any<WorkforceRoleCreatedDomainEvent>());
    }

    [Fact]
    public async Task DeleteWorkforceRole_WhenActiveStaffAssigned_ThrowsInvalidOperationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Acme Corp", "admin@acme.com", "+2348000000001");
        var role = new WorkforceRole(org.Id, "Tech Lead", null, null);
        var staff = new OrganizationMembership("usr_lead", org.Id, MembershipRoleType.Member, null, role.Id);
        dbContext.Organizations.Add(org);
        dbContext.WorkforceRoles.Add(role);
        dbContext.OrganizationMemberships.Add(staff);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);

        var handler = new DeleteWorkforceRoleCommandHandler(dbContext, orgContext, userContext, outbox);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new DeleteWorkforceRoleCommand(role.Id, org.Id), CancellationToken.None));
    }

    [Fact]
    public async Task CreateSalaryLevel_WhenValid_PersistsAndWritesOutbox()
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

        var handler = new CreateSalaryLevelCommandHandler(dbContext, orgContext, userContext, outbox);
        var levelId = await handler.Handle(new CreateSalaryLevelCommand(org.Id, "Grade A", 600000m, "NGN"), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, levelId);
        var level = await dbContext.SalaryLevels.FindAsync(levelId);
        Assert.NotNull(level);
        Assert.Equal("Grade A", level.LevelName);
        Assert.Equal(600000m, level.BaseAmount);
        outbox.Received(1).Write(Arg.Any<SalaryLevelCreatedDomainEvent>());
    }

    [Fact]
    public async Task DeleteSalaryLevel_WhenActiveStaffAssigned_ThrowsInvalidOperationException()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgContext = Substitute.For<ICurrentOrganizationContext>();
        var userContext = Substitute.For<ICurrentUserService>();
        var outbox = Substitute.For<IOutboxService>();

        var org = new Organization("Acme Corp", "admin@acme.com", "+2348000000001");
        var level = new SalaryLevel(org.Id, "Level 1", 300000m, "NGN");
        var staff = new OrganizationMembership("usr_staff", org.Id, MembershipRoleType.Member, null, null, level.Id);
        dbContext.Organizations.Add(org);
        dbContext.SalaryLevels.Add(level);
        dbContext.OrganizationMemberships.Add(staff);
        await dbContext.SaveChangesAsync();

        orgContext.HasAccessToOrganizationAsync(org.Id).Returns(true);

        var handler = new DeleteSalaryLevelCommandHandler(dbContext, orgContext, userContext, outbox);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new DeleteSalaryLevelCommand(level.Id, org.Id), CancellationToken.None));
    }
}
