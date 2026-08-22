using CebizPay.Application.Common.Interfaces.Payroll;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payroll.Enums;
using CebizPay.Infrastructure.Payroll;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace CebizPay.UnitTests.Payroll;

/// <summary>
/// Unit tests for <see cref="PayrollCalculationService"/> selection modes and deterministic salary computations.
/// </summary>
public sealed class PayrollCalculationServiceTests
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
    public async Task CalculatePayroll_WithModeAll_CalculatesAllActiveMembers()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgId = Guid.NewGuid();

        var dept = new Department(orgId, "Engineering", "ENG");
        var role = new WorkforceRole(orgId, "Software Engineer", null, "SWE");
        var level = new SalaryLevel(orgId, "Senior", 500000m, "NGN");

        dbContext.Departments.Add(dept);
        dbContext.WorkforceRoles.Add(role);
        dbContext.SalaryLevels.Add(level);

        var member1 = new OrganizationMembership("usr_1", orgId, MembershipRoleType.Member, dept.Id, role.Id, level.Id);
        var member2 = new OrganizationMembership("usr_2", orgId, MembershipRoleType.Member, dept.Id, role.Id, level.Id);

        var inactiveMember = new OrganizationMembership("usr_3", orgId, MembershipRoleType.Member);
        inactiveMember.SuspendWorkAccess("Probation");

        dbContext.OrganizationMemberships.AddRange(member1, member2, inactiveMember);
        await dbContext.SaveChangesAsync();

        var service = new PayrollCalculationService(dbContext, new NullPayrollDeductionProvider());

        var result = await service.CalculatePayrollAsync(orgId, Currency.NGN, new PayrollSelectionCriteria(PayrollSelectionMode.All));

        Assert.Equal(2, result.TotalEmployees);
        Assert.Equal(1000000m, result.TotalGrossAmount);
        Assert.Equal(0m, result.TotalDeductionsAmount);
        Assert.Equal(1000000m, result.TotalNetAmount);
    }

    [Fact]
    public async Task CalculatePayroll_WithDepartmentFilter_OnlyIncludesDepartmentMembers()
    {
        using var dbContext = CreateInMemoryDbContext();
        var orgId = Guid.NewGuid();

        var engDept = new Department(orgId, "Engineering", "ENG");
        var hrDept = new Department(orgId, "Human Resources", "HR");
        var level = new SalaryLevel(orgId, "Standard", 300000m, "NGN");

        dbContext.Departments.AddRange(engDept, hrDept);
        dbContext.SalaryLevels.Add(level);

        var memberEng = new OrganizationMembership("usr_eng", orgId, MembershipRoleType.Member, engDept.Id, null, level.Id);
        var memberHr = new OrganizationMembership("usr_hr", orgId, MembershipRoleType.Member, hrDept.Id, null, level.Id);

        dbContext.OrganizationMemberships.AddRange(memberEng, memberHr);
        await dbContext.SaveChangesAsync();

        var service = new PayrollCalculationService(dbContext, new NullPayrollDeductionProvider());

        var criteria = new PayrollSelectionCriteria(
            Mode: PayrollSelectionMode.Department,
            DepartmentIds: new[] { engDept.Id });

        var result = await service.CalculatePayrollAsync(orgId, Currency.NGN, criteria);

        Assert.Equal(1, result.TotalEmployees);
        Assert.Equal("usr_eng", result.Items[0].EmployeeUserId);
        Assert.Equal(300000m, result.TotalNetAmount);
    }
}
