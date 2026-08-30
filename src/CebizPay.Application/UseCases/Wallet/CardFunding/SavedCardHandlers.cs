using CebizPay.Application.Common.Interfaces.Payments;
using MediatR;

namespace CebizPay.Application.UseCases.Wallet.CardFunding;

/// <summary>
/// MediatR handlers for tokenized saved card operations.
/// </summary>
public sealed class SavedCardHandlers :
    IRequestHandler<GetSavedCardsQuery, IReadOnlyList<SavedCardResponseDto>>,
    IRequestHandler<GetSavedCardByIdQuery, SavedCardResponseDto?>,
    IRequestHandler<SetDefaultSavedCardCommand, SavedCardResponseDto>,
    IRequestHandler<RevokeSavedCardCommand, SavedCardResponseDto>
{
    private readonly ISavedCardService _savedCardService;

    /// <summary>
    /// Initializes a new instance of <see cref="SavedCardHandlers"/>.
    /// </summary>
    public SavedCardHandlers(ISavedCardService savedCardService)
    {
        _savedCardService = savedCardService ?? throw new ArgumentNullException(nameof(savedCardService));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<SavedCardResponseDto>> Handle(
        GetSavedCardsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _savedCardService.GetSavedCardsForUserAsync(request.CurrentUserId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SavedCardResponseDto?> Handle(
        GetSavedCardByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _savedCardService.GetSavedCardByIdAsync(request.CardId, request.CurrentUserId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SavedCardResponseDto> Handle(
        SetDefaultSavedCardCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _savedCardService.SetDefaultCardAsync(request.CardId, request.CurrentUserId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SavedCardResponseDto> Handle(
        RevokeSavedCardCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _savedCardService.RevokeSavedCardAsync(request.CardId, request.CurrentUserId, cancellationToken);
    }
}
