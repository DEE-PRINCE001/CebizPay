using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.Common.Interfaces.Payroll;

/// <summary>
/// Extensibility provider abstraction for computing non-statutory or pre-approved deductions (e.g. Loan repayments).
/// Keeps loan business rules isolated from the payroll calculation engine.
/// </summary>
public interface IPayrollDeductionProvider
{
    /// <summary>
    /// Computes and returns eligible payroll deductions for an employee.
    /// </summary>
    Task<IReadOnlyList<PayrollDeductionDetailDto>> GetDeductionsForEmployeeAsync(
        Guid organizationId,
        string employeeUserId,
        decimal grossSalary,
        Currency currency,
        CancellationToken cancellationToken = default);
}
