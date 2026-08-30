using CebizPay.Application.Common.Interfaces.Payments;
using MediatR;

namespace CebizPay.Application.UseCases.Wallet.CardFunding;

/// <summary>
/// MediatR handlers for card refund operations.
/// </summary>
public sealed class CardRefundHandlers :
    IRequestHandler<RequestCardRefundCommand, CardRefundResponseDto>,
    IRequestHandler<GetCardRefundByIdQuery, CardRefundResponseDto?>,
    IRequestHandler<ReconcileCardRefundCommand, CardRefundResponseDto>
{
    private readonly ICardRefundService _refundService;

    /// <summary>
    /// Initializes a new instance of <see cref="CardRefundHandlers"/>.
    /// </summary>
    public CardRefundHandlers(ICardRefundService refundService)
    {
        _refundService = refundService ?? throw new ArgumentNullException(nameof(refundService));
    }

    /// <inheritdoc/>
    public Task<CardRefundResponseDto> Handle(
        RequestCardRefundCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _refundService.RequestCardRefundAsync(
            fundingTransactionId: request.FundingTransactionId,
            amount: request.Amount,
            reason: request.Reason,
            idempotencyKey: request.IdempotencyKey,
            actorUserId: request.CurrentUserId,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public Task<CardRefundResponseDto?> Handle(
        GetCardRefundByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _refundService.GetRefundByIdAsync(
            refundId: request.RefundId,
            actorUserId: request.CurrentUserId,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public Task<CardRefundResponseDto> Handle(
        ReconcileCardRefundCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _refundService.ReconcileRefundAsync(
            refundId: request.RefundId,
            cancellationToken: cancellationToken);
    }
}
