using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.Common.Interfaces.Savings;

/// <summary>
/// Vendor-neutral contract for integrating with external wealth-tech / BaaS savings providers
/// (e.g. Cowrywise, Anchor, or Sandbox simulator).
/// Outsources balance-sheet risk, yield generation, and tax compliance to licensed external partners.
/// </summary>
public interface ISavingsProvider
{
    /// <summary>
    /// Unique provider identifier (e.g. "Cowrywise", "Anchor", "Mock").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Ensures a customer identity profile and KYC mapping exist at the external provider.
    /// </summary>
    Task<ExternalCustomerResult> EnsureCustomerAsync(
        ExternalCustomerRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches live available savings/investment product rates and tenures from the provider.
    /// </summary>
    Task<IReadOnlyList<ExternalSavingsProductRate>> GetProductRatesAsync(
        Currency currency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an external savings plan, subledger account, or investment portfolio for the customer.
    /// </summary>
    Task<ExternalSavingsPlanResult> CreatePlanAsync(
        ExternalCreatePlanRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Funds / deposits capital into the external savings contract.
    /// </summary>
    Task<ExternalFundingResult> FundPlanAsync(
        ExternalFundingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the external provider for current live valuation, accrued yield, and maturity state.
    /// </summary>
    Task<ExternalSavingsPosition> GetPositionAsync(
        string externalPlanId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Liquidates (withdraws) the plan (early or upon maturity), returning net settled payout and penalty figures.
    /// </summary>
    Task<ExternalLiquidationResult> LiquidatePlanAsync(
        ExternalLiquidationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses and verifies an inbound webhook from the provider into a standardized CebizPay event.
    /// </summary>
    Task<SavingsWebhookEvent?> ParseWebhookAsync(
        string rawPayload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default);
}
