using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Payments.Events;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Payments.Funding;

/// <summary>
/// Infrastructure service implementation managing tokenized saved cards.
/// Strictly handles only provider tokens and truncated last4 digits.
/// Never receives, stores, or handles raw card credentials.
/// </summary>
public sealed class SavedCardService : ISavedCardService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IOutboxService _outbox;
    private readonly ILogger<SavedCardService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SavedCardService"/> class.
    /// </summary>
    public SavedCardService(
        ApplicationDbContext dbContext,
        IOutboxService outbox,
        ILogger<SavedCardService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<SavedCardResponseDto> SaveCardTokenAsync(
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
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (walletId == Guid.Empty)
            throw new ArgumentException("WalletId is required.", nameof(walletId));
        if (string.IsNullOrWhiteSpace(providerToken))
            throw new ArgumentException("ProviderToken is required.", nameof(providerToken));
        if (string.IsNullOrWhiteSpace(last4))
            throw new ArgumentException("Last4 is required.", nameof(last4));

        var cleanUserId = userId.Trim();
        var cleanToken = providerToken.Trim();
        var cleanLast4 = last4.Trim();

        // Check if an existing card token exists for this user and provider
        var existingCard = await _dbContext.SavedCards
            .FirstOrDefaultAsync(c => c.UserId == cleanUserId && c.Provider == provider && c.ProviderToken == cleanToken, cancellationToken)
            .ConfigureAwait(false);

        if (isDefault)
        {
            var userCards = await _dbContext.SavedCards
                .Where(c => c.UserId == cleanUserId && c.IsDefault)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var card in userCards)
            {
                card.SetDefault(false);
            }
        }

        SavedCard targetCard;
        if (existingCard != null)
        {
            if (isDefault)
            {
                existingCard.SetDefault(true);
            }
            targetCard = existingCard;
        }
        else
        {
            // If user has no existing cards, make this one default
            var hasOtherCards = await _dbContext.SavedCards
                .AnyAsync(c => c.UserId == cleanUserId && c.Status == SavedCardStatus.Active, cancellationToken)
                .ConfigureAwait(false);

            var makeDefault = isDefault || !hasOtherCards;

            targetCard = SavedCard.Create(
                userId: cleanUserId,
                walletId: walletId,
                provider: provider,
                providerToken: cleanToken,
                last4: cleanLast4,
                brand: brand,
                expiryMonth: expiryMonth,
                expiryYear: expiryYear,
                cardHolderName: cardHolderName,
                providerCustomerReference: providerCustomerReference,
                isDefault: makeDefault);

            _dbContext.SavedCards.Add(targetCard);
        }

        var audit = AuditLog.Create(
            actorId: cleanUserId,
            action: AuditActions.SavedCardCreated,
            resourceType: AuditResourceTypes.SavedCard,
            resourceId: targetCard.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new
            {
                targetCard.UserId,
                targetCard.WalletId,
                Provider = targetCard.Provider.ToString(),
                targetCard.Last4,
                targetCard.Brand,
                targetCard.IsDefault
            }));
        _dbContext.AuditLogs.Add(audit);

        _outbox.Write(new SavedCardCreatedDomainEvent(
            SavedCardId: targetCard.Id,
            UserId: targetCard.UserId,
            WalletId: targetCard.WalletId,
            Provider: targetCard.Provider,
            Last4: targetCard.Last4,
            Brand: targetCard.Brand,
            OccurredOnUtc: DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToDto(targetCard);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SavedCardResponseDto>> GetSavedCardsForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));

        var cleanUserId = userId.Trim();
        var cards = await _dbContext.SavedCards
            .AsNoTracking()
            .Where(c => c.UserId == cleanUserId && c.Status == SavedCardStatus.Active)
            .OrderByDescending(c => c.IsDefault)
            .ThenByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return cards.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<SavedCardResponseDto?> GetSavedCardByIdAsync(
        Guid cardId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (cardId == Guid.Empty)
            throw new ArgumentException("CardId is required.", nameof(cardId));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));

        var cleanUserId = userId.Trim();
        var card = await _dbContext.SavedCards
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cardId && c.UserId == cleanUserId, cancellationToken)
            .ConfigureAwait(false);

        return card == null ? null : MapToDto(card);
    }

    /// <inheritdoc/>
    public async Task<SavedCardResponseDto> SetDefaultCardAsync(
        Guid cardId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (cardId == Guid.Empty)
            throw new ArgumentException("CardId is required.", nameof(cardId));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));

        var cleanUserId = userId.Trim();
        var card = await _dbContext.SavedCards
            .FirstOrDefaultAsync(c => c.Id == cardId && c.UserId == cleanUserId, cancellationToken)
            .ConfigureAwait(false);

        if (card == null)
            throw new InvalidOperationException($"SavedCard '{cardId}' not found for user.");

        if (card.Status != SavedCardStatus.Active)
            throw new InvalidOperationException($"Cannot set card as default because card status is {card.Status}.");

        var userCards = await _dbContext.SavedCards
            .Where(c => c.UserId == cleanUserId && c.IsDefault)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var c in userCards)
        {
            c.SetDefault(false);
        }

        card.SetDefault(true);

        var audit = AuditLog.Create(
            actorId: cleanUserId,
            action: AuditActions.SavedCardDefaultSet,
            resourceType: AuditResourceTypes.SavedCard,
            resourceId: card.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new { card.Id, card.Last4, card.IsDefault }));
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToDto(card);
    }

    /// <inheritdoc/>
    public async Task<SavedCardResponseDto> RevokeSavedCardAsync(
        Guid cardId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (cardId == Guid.Empty)
            throw new ArgumentException("CardId is required.", nameof(cardId));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));

        var cleanUserId = userId.Trim();
        var card = await _dbContext.SavedCards
            .FirstOrDefaultAsync(c => c.Id == cardId && c.UserId == cleanUserId, cancellationToken)
            .ConfigureAwait(false);

        if (card == null)
            throw new InvalidOperationException($"SavedCard '{cardId}' not found for user.");

        card.Revoke();

        var audit = AuditLog.Create(
            actorId: cleanUserId,
            action: AuditActions.SavedCardRevoked,
            resourceType: AuditResourceTypes.SavedCard,
            resourceId: card.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new { card.Id, card.Last4, Status = card.Status.ToString() }));
        _dbContext.AuditLogs.Add(audit);

        _outbox.Write(new SavedCardRevokedDomainEvent(
            SavedCardId: card.Id,
            UserId: card.UserId,
            Provider: card.Provider,
            OccurredOnUtc: DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToDto(card);
    }

    private static SavedCardResponseDto MapToDto(SavedCard card) =>
        new(
            Id: card.Id,
            UserId: card.UserId,
            WalletId: card.WalletId,
            Provider: card.Provider.ToString(),
            Last4: card.Last4,
            Brand: card.Brand,
            ExpiryMonth: card.ExpiryMonth,
            ExpiryYear: card.ExpiryYear,
            CardHolderName: card.CardHolderName,
            Status: card.Status.ToString(),
            IsDefault: card.IsDefault,
            CreatedAtUtc: card.CreatedAtUtc,
            UpdatedAtUtc: card.UpdatedAtUtc);
}
