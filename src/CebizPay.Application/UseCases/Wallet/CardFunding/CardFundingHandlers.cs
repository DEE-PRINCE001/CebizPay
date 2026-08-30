using CebizPay.Application.Common.Interfaces.Payments;
using MediatR;

namespace CebizPay.Application.UseCases.Wallet.CardFunding;

/// <summary>
/// MediatR handlers for card funding operations.
/// </summary>
public sealed class CardFundingHandlers :
    IRequestHandler<InitializeCardFundingCommand, CardFundingInitializationResponse>,
    IRequestHandler<ChargeSavedCardCommand, ChargeSavedCardResponseDto>,
    IRequestHandler<ReconcileCardFundingCommand, PaymentProviderResult>
{
    private readonly ICardFundingService _cardFundingService;

    /// <summary>
    /// Initializes a new instance of <see cref="CardFundingHandlers"/>.
    /// </summary>
    public CardFundingHandlers(ICardFundingService cardFundingService)
    {
        _cardFundingService = cardFundingService ?? throw new ArgumentNullException(nameof(cardFundingService));
    }

    /// <inheritdoc/>
    public Task<CardFundingInitializationResponse> Handle(
        InitializeCardFundingCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _cardFundingService.InitializeCardFundingAsync(
            walletId: request.WalletId,
            amount: request.Amount,
            currency: request.Currency,
            provider: request.Provider,
            callbackUrl: request.CallbackUrl,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ChargeSavedCardResponseDto> Handle(
        ChargeSavedCardCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _cardFundingService.ChargeSavedCardAsync(
            savedCardId: request.SavedCardId,
            amount: request.Amount,
            currency: request.Currency,
            idempotencyKey: request.IdempotencyKey,
            actorUserId: request.CurrentUserId,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PaymentProviderResult> Handle(
        ReconcileCardFundingCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _cardFundingService.ReconcileCardFundingAsync(
            fundingTransactionId: request.FundingTransactionId,
            cancellationToken: cancellationToken);
    }
}
