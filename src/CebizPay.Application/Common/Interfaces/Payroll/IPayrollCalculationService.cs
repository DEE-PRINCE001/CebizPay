using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.Common.Interfaces.Payroll;

/// <summary>
/// Domain service for calculating deterministic payroll line-items without performing financial mutations.
/// </summary>
public interface IPayrollCalculationService
{
    /// <summary>
    /// Computes payroll gross amounts, applies eligible deductions, and produces a complete dry-run calculation preview.
    /// </summary>
    Task<PayrollCalculationResultDto> CalculatePayrollAsync(
        Guid organizationId,
        Currency currency,
        PayrollSelectionCriteria criteria,
        CancellationToken cancellationToken = default);
}
