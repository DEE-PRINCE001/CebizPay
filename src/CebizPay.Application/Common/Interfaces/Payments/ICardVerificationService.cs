using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Service contract for orchestrating card zero-auth / micro-charge verification workflows.
/// </summary>
public interface ICardVerificationService
{
    /// <summary>
    /// Initializes a card verification session (zero-auth or nominal micro-charge).
    /// </summary>
    Task<CardVerificationResponseDto> InitializeCardVerificationAsync(
        Guid walletId,
        string userId,
        string email,
        string callbackUrl,
        PaymentProvider? preferredProvider = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the verification session, tokenizes the verified card, and initiates micro-charge refund if applicable.
    /// </summary>
    Task<CardVerificationResponseDto> CompleteCardVerificationAsync(
        string reference,
        CancellationToken cancellationToken = default);
}
