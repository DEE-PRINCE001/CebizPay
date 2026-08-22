namespace CebizPay.Application.Common.Interfaces.Payroll;

/// <summary>
/// Service executing single-item atomic financial disbursements within isolated PostgreSQL database transactions.
/// </summary>
public interface IPayrollExecutionService
{
    /// <summary>
    /// Executes a single payroll item atomically:
    /// 1. Row-level locks organization and employee wallets.
    /// 2. Verifies sufficiency of organization available balance.
    /// 3. Posts balanced double-entry ledger entries (Debit Org Expense, Credit Employee Wallet).
    /// 4. Generates PaymentVoucher, writes AuditLog and Outbox events.
    /// 5. Marks PayrollItem as Completed or Failed.
    /// </summary>
    Task<PayrollItemExecutionResult> ExecutePayrollItemAsync(
        Guid payrollItemId,
        string workerId,
        CancellationToken cancellationToken = default);
}
