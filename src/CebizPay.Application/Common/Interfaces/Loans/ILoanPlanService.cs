namespace CebizPay.Application.Common.Interfaces.Loans;

/// <summary>
/// Service contract for managing corporate loan plan definitions within an organization tenant.
/// </summary>
public interface ILoanPlanService
{
    /// <summary>
    /// Creates a new corporate loan plan.
    /// </summary>
    Task<CorporateLoanPlanDto> CreatePlanAsync(
        Guid organizationId,
        CreateLoanPlanRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing corporate loan plan.
    /// </summary>
    Task<CorporateLoanPlanDto> UpdatePlanAsync(
        Guid organizationId,
        Guid planId,
        UpdateLoanPlanRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all corporate loan plans for an organization tenant.
    /// </summary>
    Task<IReadOnlyList<CorporateLoanPlanDto>> GetPlansForOrgAsync(
        Guid organizationId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single corporate loan plan by ID.
    /// </summary>
    Task<CorporateLoanPlanDto?> GetPlanByIdAsync(
        Guid organizationId,
        Guid planId,
        CancellationToken cancellationToken = default);
}
