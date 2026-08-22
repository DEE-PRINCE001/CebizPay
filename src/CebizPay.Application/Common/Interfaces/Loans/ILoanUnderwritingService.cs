namespace CebizPay.Application.Common.Interfaces.Loans;

/// <summary>
/// Service contract for deterministic staff loan underwriting and 33% Debt-To-Income (DTI) compliance evaluation.
/// </summary>
public interface ILoanUnderwritingService
{
    /// <summary>
    /// Evaluates applicant's verified monthly salary against existing debt obligations and proposed loan payment.
    /// Invariant: TotalMonthlyDebt (Existing + Proposed) &lt;= 0.33 * VerifiedMonthlySalary.
    /// </summary>
    Task<UnderwritingEvaluationResult> UnderwriteApplicationAsync(
        Guid organizationId,
        string applicantUserId,
        decimal requestedAmount,
        decimal annualInterestRate,
        int durationMonths,
        CancellationToken cancellationToken = default);
}
