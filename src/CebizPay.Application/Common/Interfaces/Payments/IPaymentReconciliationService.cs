namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Service responsible for querying external payment providers and reconciling in-flight,
/// unknown, or pending payment attempts with the CebizPay financial state.
/// </summary>
public interface IPaymentReconciliationService
{
    /// <summary>
    /// Reconciles a specific <see cref="CebizPay.Domain.Payments.Entities.PaymentAttempt"/> by querying the provider's status endpoint.
    /// </summary>
    /// <param name="paymentAttemptId">Identifier of the payment attempt to reconcile.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reconciled <see cref="PaymentProviderResult"/>.</returns>
    Task<PaymentProviderResult> ReconcilePaymentAttemptAsync(
        Guid paymentAttemptId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans for unresolved in-flight attempts (status <see cref="CebizPay.Domain.Payments.Enums.PaymentAttemptStatus.Unknown"/>
    /// or stale <see cref="CebizPay.Domain.Payments.Enums.PaymentAttemptStatus.Processing"/>) and reconciles them.
    /// </summary>
    /// <param name="batchSize">Maximum number of attempts to reconcile in this batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of attempts successfully resolved to a terminal state.</returns>
    Task<int> ReconcileUnresolvedAttemptsAsync(
        int batchSize = 50,
        CancellationToken cancellationToken = default);
}
