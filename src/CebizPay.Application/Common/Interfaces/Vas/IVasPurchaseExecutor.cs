using CebizPay.Application.Common.Models.Vas;

namespace CebizPay.Application.Common.Interfaces.Vas;

/// <summary>
/// Service responsible for dispatching VAS fulfillment to external providers (VTUGATE)
/// and updating transaction status, ledger reversals, or scheduling reconciliation.
/// </summary>
public interface IVasPurchaseExecutor
{
    /// <summary>
    /// Executes the external provider purchase for a pending VAS transaction.
    /// Manages state transitions, automated ledger reversal on definitive failure, and outbox event publishing.
    /// </summary>
    Task<VasPurchaseResult> ExecutePurchaseAsync(Guid vasTransactionId, CancellationToken cancellationToken = default);
}
