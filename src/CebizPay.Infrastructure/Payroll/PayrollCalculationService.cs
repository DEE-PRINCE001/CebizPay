using CebizPay.Application.Common.Interfaces.Payroll;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payroll.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Payroll;

/// <summary>
/// Domain service calculating deterministic payroll line-items without persisting changes or mutating balances.
/// </summary>
public sealed class PayrollCalculationService : IPayrollCalculationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPayrollDeductionProvider _deductionProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="PayrollCalculationService"/>.
    /// </summary>
    public PayrollCalculationService(
        ApplicationDbContext dbContext,
        IPayrollDeductionProvider deductionProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _deductionProvider = deductionProvider ?? throw new ArgumentNullException(nameof(deductionProvider));
    }

    /// <inheritdoc/>
    public async Task<PayrollCalculationResultDto> CalculatePayrollAsync(
        Guid organizationId,
        Currency currency,
        PayrollSelectionCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));

        currency.EnsureTransactionalV1();
        criteria ??= new PayrollSelectionCriteria();

        // 1. Query all active organization memberships
        var query = _dbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.Status == MembershipStatus.Active);

        // Apply Selection Mode filtering
        query = criteria.Mode switch
        {
            PayrollSelectionMode.Department => criteria.DepartmentIds != null && criteria.DepartmentIds.Count > 0
                ? query.Where(m => m.DepartmentId.HasValue && criteria.DepartmentIds.Contains(m.DepartmentId.Value))
                : query,
            PayrollSelectionMode.Role => criteria.WorkforceRoleIds != null && criteria.WorkforceRoleIds.Count > 0
                ? query.Where(m => m.WorkforceRoleId.HasValue && criteria.WorkforceRoleIds.Contains(m.WorkforceRoleId.Value))
                : query,
            PayrollSelectionMode.Level => criteria.SalaryLevelIds != null && criteria.SalaryLevelIds.Count > 0
                ? query.Where(m => m.SalaryLevelId.HasValue && criteria.SalaryLevelIds.Contains(m.SalaryLevelId.Value))
                : query,
            PayrollSelectionMode.Individual => criteria.EmployeeUserIds != null && criteria.EmployeeUserIds.Count > 0
                ? query.Where(m => criteria.EmployeeUserIds.Contains(m.UserId))
                : query,
            _ => query // All
        };

        var memberships = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        if (memberships.Count == 0)
        {
            return new PayrollCalculationResultDto(
                organizationId,
                currency,
                TotalEmployees: 0,
                TotalGrossAmount: 0m,
                TotalDeductionsAmount: 0m,
                TotalNetAmount: 0m,
                Items: Array.Empty<PayrollCalculationItemDto>());
        }

        var userIds = memberships.Select(m => m.UserId).Distinct().ToList();
        var departmentIds = memberships.Where(m => m.DepartmentId.HasValue).Select(m => m.DepartmentId!.Value).Distinct().ToList();
        var roleIds = memberships.Where(m => m.WorkforceRoleId.HasValue).Select(m => m.WorkforceRoleId!.Value).Distinct().ToList();
        var salaryLevelIds = memberships.Where(m => m.SalaryLevelId.HasValue).Select(m => m.SalaryLevelId!.Value).Distinct().ToList();

        // 2. Fetch lookup data in parallel batches
        var profiles = await _dbContext.IndividualProfiles
            .AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, cancellationToken)
            .ConfigureAwait(false);

        var departments = await _dbContext.Departments
            .AsNoTracking()
            .Where(d => departmentIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken)
            .ConfigureAwait(false);

        var roles = await _dbContext.WorkforceRoles
            .AsNoTracking()
            .Where(r => roleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken)
            .ConfigureAwait(false);

        var salaryLevels = await _dbContext.SalaryLevels
            .AsNoTracking()
            .Where(s => salaryLevelIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken)
            .ConfigureAwait(false);

        var items = new List<PayrollCalculationItemDto>(memberships.Count);
        decimal totalGross = 0m;
        decimal totalDeductions = 0m;
        decimal totalNet = 0m;

        // 3. Process each eligible membership line
        foreach (var membership in memberships)
        {
            profiles.TryGetValue(membership.UserId, out var profile);
            var empName = profile != null
                ? $"{profile.FirstName} {profile.LastName}".Trim()
                : $"Employee {membership.UserId[..Math.Min(8, membership.UserId.Length)]}";
            var empEmail = $"{membership.UserId}@cebizpay.internal";

            departments.TryGetValue(membership.DepartmentId ?? Guid.Empty, out var dept);
            roles.TryGetValue(membership.WorkforceRoleId ?? Guid.Empty, out var role);
            salaryLevels.TryGetValue(membership.SalaryLevelId ?? Guid.Empty, out var salaryLevel);

            var grossPay = salaryLevel?.BaseAmount ?? 0m;

            // Fetch deductions from extensible provider
            var deductions = await _deductionProvider.GetDeductionsForEmployeeAsync(
                organizationId,
                membership.UserId,
                grossPay,
                currency,
                cancellationToken).ConfigureAwait(false);

            var deductionsAmount = deductions != null && deductions.Count > 0
                ? deductions.Sum(d => d.Amount)
                : 0m;

            var netPay = grossPay - deductionsAmount;
            if (netPay < 0)
            {
                throw new InvalidOperationException(
                    $"Deductions ({deductionsAmount:F2}) exceed gross salary ({grossPay:F2}) for employee '{empName}' ({membership.UserId}). Net pay cannot be negative.");
            }

            totalGross += grossPay;
            totalDeductions += deductionsAmount;
            totalNet += netPay;

            items.Add(new PayrollCalculationItemDto(
                EmployeeUserId: membership.UserId,
                EmployeeName: empName,
                EmployeeEmail: empEmail,
                DepartmentId: membership.DepartmentId,
                DepartmentName: dept?.Name,
                WorkforceRoleId: membership.WorkforceRoleId,
                RoleTitle: role?.Title,
                SalaryLevelId: membership.SalaryLevelId,
                SalaryLevelName: salaryLevel?.LevelName,
                GrossPay: grossPay,
                TotalDeductions: deductionsAmount,
                NetPay: netPay,
                Currency: currency,
                Deductions: deductions));
        }

        return new PayrollCalculationResultDto(
            OrganizationId: organizationId,
            Currency: currency,
            TotalEmployees: items.Count,
            TotalGrossAmount: totalGross,
            TotalDeductionsAmount: totalDeductions,
            TotalNetAmount: totalNet,
            Items: items);
    }
}
