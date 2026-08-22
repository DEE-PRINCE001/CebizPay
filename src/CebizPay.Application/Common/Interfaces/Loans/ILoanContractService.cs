namespace CebizPay.Application.Common.Interfaces.Loans;

/// <summary>
/// Service contract for querying loan contracts, schedules, and executing staff offboarding loan conversions.
/// </summary>
public interface ILoanContractService
{
    /// <summary>
    /// Retrieves a loan contract with its repayment schedule by ID.
    /// </summary>
    Task<LoanContractDto?> GetContractByIdAsync(
        Guid organizationId,
        Guid contractId,
        string? requestingUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all loan contracts for an organization tenant.
    /// </summary>
    Task<IReadOnlyList<LoanContractDto>> GetContractsForOrgAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all loan contracts for a specific borrower.
    /// </summary>
    Task<IReadOnlyList<LoanContractDto>> GetContractsForUserAsync(
        string borrowerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts all outstanding corporate payroll loans for a departing/terminated staff member into standard individual loans.
    /// </summary>
    Task<IReadOnlyList<LoanContractDto>> ConvertTerminatedStaffLoansAsync(
        Guid organizationId,
        string staffUserId,
        string reason,
        string actorUserId,
        CancellationToken cancellationToken = default);
}
