using CebizPay.Application.Common.Models.Vas;

namespace CebizPay.Application.Common.Interfaces.Vas;

/// <summary>
/// Service responsible for reconciling in-flight, unknown, or timed-out VAS transactions with external providers.
/// </summary>
public interface IVasReconciliationService
{
    /// <summary>
    /// Reconciles a specific VAS transaction by querying the provider gateway.
    /// </summary>
    Task<VasPurchaseProviderResult> ReconcileVasTransactionAsync(Guid vasTransactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries and reconciles a batch of unresolved/unknown VAS transactions.
    /// </summary>
    Task<int> ReconcileUnresolvedVasTransactionsAsync(int batchSize = 50, CancellationToken cancellationToken = default);
}
