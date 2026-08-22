using CebizPay.Application.Common.Interfaces.Payroll;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Infrastructure.Payroll;

/// <summary>
/// Default deduction provider returning an empty deduction list for standard payroll.
/// Serves as the clean architectural boundary for Phase 4B Loan deductions.
/// </summary>
public sealed class NullPayrollDeductionProvider : IPayrollDeductionProvider
{
    private static readonly IReadOnlyList<PayrollDeductionDetailDto> EmptyList = Array.Empty<PayrollDeductionDetailDto>();

    /// <inheritdoc/>
    public Task<IReadOnlyList<PayrollDeductionDetailDto>> GetDeductionsForEmployeeAsync(
        Guid organizationId,
        string employeeUserId,
        decimal grossSalary,
        Currency currency,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(EmptyList);
    }
}
