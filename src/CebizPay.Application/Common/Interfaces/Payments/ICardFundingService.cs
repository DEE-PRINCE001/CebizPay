using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Service contract for orchestrating card-based wallet funding sessions and reconciliation.
/// </summary>
public interface ICardFundingService
{
    /// <summary>
    /// Initializes a card funding checkout session.
    /// </summary>
    Task<CardFundingInitializationResponse> InitializeCardFundingAsync(
        Guid walletId,
        decimal amount,
        Currency currency,
        PaymentProvider provider,
        string callbackUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles an in-flight card funding transaction with the provider.
    /// </summary>
    Task<PaymentProviderResult> ReconcileCardFundingAsync(
        Guid fundingTransactionId,
        CancellationToken cancellationToken = default);
}
