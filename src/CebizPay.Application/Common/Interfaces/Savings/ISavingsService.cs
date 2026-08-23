namespace CebizPay.Application.Common.Interfaces.Savings;

/// <summary>
/// Service contract managing lifecycle, subscriptions, contributions, interest calculations, and withdrawals for savings products.
/// </summary>
public interface ISavingsService
{
    /// <summary>
    /// Computes deterministic preview metrics (estimated total interest, maturity payout, early exit penalty) for a prospective plan.
    /// </summary>
    Task<SavingsPreviewResult> PreviewSavingsAsync(SavingsPreviewRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new Savings Plan (Individual or Corporate).
    /// </summary>
    Task<SavingsPlanDto> CreatePlanAsync(string createdByUserId, CreateSavingsPlanRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a Savings Plan by ID.
    /// </summary>
    Task<SavingsPlanDto?> GetPlanByIdAsync(Guid planId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists active savings plans available for individual or organization subscription.
    /// </summary>
    Task<IReadOnlyList<SavingsPlanDto>> GetAvailablePlansAsync(Guid? organizationId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a new Savings Account instance, debits the initial deposit from the user's wallet, and posts to the central ledger.
    /// </summary>
    Task<SavingsAccountDto> OpenAccountAsync(string ownerUserId, OpenSavingsAccountRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a Savings Account by ID.
    /// </summary>
    Task<SavingsAccountDto?> GetAccountByIdAsync(Guid accountId, string requesterUserId, Guid? organizationId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists savings accounts owned by a user or sponsored by an organization.
    /// </summary>
    Task<IReadOnlyList<SavingsAccountDto>> GetAccountsAsync(string? ownerUserId = null, Guid? organizationId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an atomic contribution to an active savings account, debiting the customer wallet and crediting the platform savings pool.
    /// </summary>
    Task<SavingsAccountDto> ContributeAsync(Guid accountId, string ownerUserId, decimal amount, string? idempotencyKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates withdrawal terms (maturity vs early penalty) for a savings account as of current timestamp.
    /// </summary>
    Task<SavingsPreviewResult> PreviewWithdrawalAsync(Guid accountId, string ownerUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an atomic withdrawal from a savings account, crediting the customer wallet with net payout and posting double-entry ledger entries.
    /// </summary>
    Task<SavingsWithdrawalResultDto> WithdrawAsync(Guid accountId, string ownerUserId, string? idempotencyKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs daily interest accrual across all active eligible savings accounts. (Invoked by background worker).
    /// </summary>
    Task<int> ProcessDailyInterestAccrualAsync(DateTime accrualDate, CancellationToken cancellationToken = default);
}
