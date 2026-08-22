using CebizPay.Application.Common.Interfaces.Payroll;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Loans.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Payroll;

/// <summary>
/// Payroll deduction provider implementation integrating approved Corporate Payroll Loan repayments into payroll calculations.
/// Evaluates active corporate loan obligations, queries earliest pending/due repayment schedule installments,
/// and provides deterministic deduction line items before net pay calculation.
/// </summary>
public sealed class PayrollLoanDeductionProvider : IPayrollDeductionProvider
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollLoanDeductionProvider"/> class.
    /// </summary>
    public PayrollLoanDeductionProvider(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PayrollDeductionDetailDto>> GetDeductionsForEmployeeAsync(
        Guid organizationId,
        string employeeUserId,
        decimal grossSalary,
        Currency currency,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch active corporate payroll loan contracts for this employee in this organization
        var activeLoans = await _dbContext.LoanContracts
            .Include(c => c.RepaymentSchedule)
            .AsNoTracking()
            .Where(c => c.OrganizationId == organizationId &&
                        c.BorrowerUserId == employeeUserId &&
                        c.LoanType == LoanType.CorporatePayrollLoan &&
                        (c.Status == LoanContractStatus.Active || c.Status == LoanContractStatus.Overdue))
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (activeLoans.Count == 0)
        {
            return Array.Empty<PayrollDeductionDetailDto>();
        }

        var deductions = new List<PayrollDeductionDetailDto>();

        foreach (var loan in activeLoans)
        {
            // Pick earliest unpaid installment for this loan obligation
            var nextInstallment = loan.RepaymentSchedule
                .Where(i => i.Status == LoanRepaymentStatus.Pending || i.Status == LoanRepaymentStatus.Due || i.Status == LoanRepaymentStatus.Missed)
                .OrderBy(i => i.DueDate)
                .ThenBy(i => i.InstallmentNumber)
                .FirstOrDefault();

            if (nextInstallment != null && nextInstallment.ScheduledAmount > 0)
            {
                var remainingDue = nextInstallment.ScheduledAmount - nextInstallment.PaidAmount;
                if (remainingDue > 0)
                {
                    deductions.Add(new PayrollDeductionDetailDto(
                        DeductionType: "CORPORATE_LOAN_REPAYMENT",
                        Amount: remainingDue,
                        Reference: nextInstallment.Id.ToString(),
                        Description: $"Corporate Loan Repayment (Installment #{nextInstallment.InstallmentNumber} for {loan.ContractReference})"));
                }
            }
        }

        return deductions;
    }
}
