namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Failover coordinator responsible for evaluating failed payment attempts and orchestrating
/// fallback provider dispatch in accordance with locked provider failover rules.
/// </summary>
public interface IPaymentFailoverService
{
    /// <summary>
    /// Evaluates the existing payment attempts for the given <paramref name="ledgerTransactionId"/>
    /// and executes a fallback attempt if permitted (i.e. primary provider failed with TechnicalFailure).
    /// </summary>
    /// <param name="ledgerTransactionId">Financial transaction identifier to evaluate for failover.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="PaymentFailoverResult"/> containing failover status and fallback attempt identifier.</returns>
    Task<PaymentFailoverResult> FailoverAsync(
        Guid ledgerTransactionId,
        CancellationToken cancellationToken = default);
}
