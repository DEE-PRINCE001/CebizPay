namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Service contract for executing and reconciling card payment refunds.
/// </summary>
public interface ICardRefundService
{
    /// <summary>
    /// Initiates a card refund, executes provider refund, and reverses the double-entry ledger.
    /// </summary>
    Task<CardRefundResponseDto> RequestCardRefundAsync(
        Guid fundingTransactionId,
        decimal amount,
        string reason,
        string idempotencyKey,
        string actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a card refund record by ID.
    /// </summary>
    Task<CardRefundResponseDto?> GetRefundByIdAsync(
        Guid refundId,
        string actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles an in-flight refund status with the provider gateway.
    /// </summary>
    Task<CardRefundResponseDto> ReconcileRefundAsync(
        Guid refundId,
        CancellationToken cancellationToken = default);
}
