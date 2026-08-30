using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Service contract for orchestrating card-based wallet funding sessions, saved card charges, and reconciliation.
/// </summary>
public interface ICardFundingService
{
    /// <summary>
    /// Initializes a hosted card funding checkout session.
    /// </summary>
    Task<CardFundingInitializationResponse> InitializeCardFundingAsync(
        Guid walletId,
        decimal amount,
        Currency currency,
        PaymentProvider? provider,
        string callbackUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Charges a tokenized saved card directly to fund the customer wallet.
    /// </summary>
    Task<ChargeSavedCardResponseDto> ChargeSavedCardAsync(
        Guid savedCardId,
        decimal amount,
        Currency currency,
        string idempotencyKey,
        string actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles an in-flight card funding transaction with the provider.
    /// </summary>
    Task<PaymentProviderResult> ReconcileCardFundingAsync(
        Guid fundingTransactionId,
        CancellationToken cancellationToken = default);
}
