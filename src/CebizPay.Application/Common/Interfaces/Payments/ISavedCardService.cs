using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Service contract for managing tokenized saved cards.
/// </summary>
public interface ISavedCardService
{
    /// <summary>
    /// Stores or updates a tokenized card returned from provider checkout or verification.
    /// </summary>
    Task<SavedCardResponseDto> SaveCardTokenAsync(
        string userId,
        Guid walletId,
        PaymentProvider provider,
        string providerToken,
        string last4,
        string brand,
        string? expiryMonth = null,
        string? expiryYear = null,
        string? cardHolderName = null,
        string? providerCustomerReference = null,
        bool isDefault = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active saved cards belonging to the specified user.
    /// </summary>
    Task<IReadOnlyList<SavedCardResponseDto>> GetSavedCardsForUserAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific saved card by ID, enforcing user ownership.
    /// </summary>
    Task<SavedCardResponseDto?> GetSavedCardByIdAsync(
        Guid cardId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a saved card as the default payment method for the user.
    /// </summary>
    Task<SavedCardResponseDto> SetDefaultCardAsync(
        Guid cardId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a saved card, disabling it for future charges.
    /// </summary>
    Task<SavedCardResponseDto> RevokeSavedCardAsync(
        Guid cardId,
        string userId,
        CancellationToken cancellationToken = default);
}
