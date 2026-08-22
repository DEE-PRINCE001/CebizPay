namespace CebizPay.Application.Common.Interfaces.Loans;

/// <summary>
/// Service contract for managing the staff loan application lifecycle (preview, submission, review, approval, decline).
/// </summary>
public interface ILoanApplicationService
{
    /// <summary>
    /// Computes a preview of loan terms and DTI compliance before submission.
    /// </summary>
    Task<LoanCalculationPreviewDto> PreviewApplicationAsync(
        Guid organizationId,
        string applicantUserId,
        LoanCalculationPreviewRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a staff loan application with snapshotted underwriting metrics.
    /// </summary>
    Task<LoanApplicationDto> SubmitApplicationAsync(
        Guid organizationId,
        string applicantUserId,
        SubmitLoanApplicationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a loan application by ID.
    /// </summary>
    Task<LoanApplicationDto?> GetApplicationByIdAsync(
        Guid organizationId,
        Guid applicationId,
        string? requestingUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all loan applications for an organization tenant.
    /// </summary>
    Task<IReadOnlyList<LoanApplicationDto>> GetApplicationsForOrgAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all loan applications submitted by a specific user.
    /// </summary>
    Task<IReadOnlyList<LoanApplicationDto>> GetApplicationsForUserAsync(
        string applicantUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Formally approves an application, generates loan contract, builds repayment schedule, and initiates wallet disbursement.
    /// </summary>
    Task<LoanContractDto> ApproveApplicationAsync(
        Guid organizationId,
        Guid applicationId,
        string approverUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Formally declines a staff loan application with recorded rationale.
    /// </summary>
    Task<LoanApplicationDto> DeclineApplicationAsync(
        Guid organizationId,
        Guid applicationId,
        string deciderUserId,
        string reason,
        CancellationToken cancellationToken = default);
}
