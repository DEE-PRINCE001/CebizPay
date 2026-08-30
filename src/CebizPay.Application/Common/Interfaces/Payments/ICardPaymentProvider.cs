using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Provider-neutral abstraction for hosted/tokenized card payment processing,
/// saved card charges, refunds, and verification.
/// Note: CebizPay never stores or handles raw PAN/CVV/PIN credentials directly.
/// </summary>
public interface ICardPaymentProvider
{
    /// <summary>The payment provider identifier implemented by this adapter.</summary>
    PaymentProvider Provider { get; }

    /// <summary>
    /// Initializes a card payment checkout session with the external provider.
    /// </summary>
    Task<CardPaymentInitializationResult> InitializeCardPaymentAsync(
        CardPaymentInitializationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the external status of a card payment using the transaction reference.
    /// </summary>
    Task<PaymentProviderResult> GetCardPaymentStatusAsync(
        string providerReference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Charges an existing tokenized payment method directly via the provider API.
    /// </summary>
    Task<CardChargeResult> ChargeSavedCardAsync(
        CardSavedChargeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates a card payment refund on the external gateway.
    /// </summary>
    Task<CardRefundResult> RefundCardPaymentAsync(
        CardRefundRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes a card verification session (zero-auth or nominal micro-charge).
    /// </summary>
    Task<CardVerificationResult> VerifyCardAsync(
        CardVerificationRequest request,
        CancellationToken cancellationToken = default);
}
