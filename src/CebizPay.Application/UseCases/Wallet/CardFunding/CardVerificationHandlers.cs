using CebizPay.Application.Common.Interfaces.Payments;
using MediatR;

namespace CebizPay.Application.UseCases.Wallet.CardFunding;

/// <summary>
/// MediatR handlers for card verification operations.
/// </summary>
public sealed class CardVerificationHandlers :
    IRequestHandler<InitializeCardVerificationCommand, CardVerificationResponseDto>,
    IRequestHandler<CompleteCardVerificationCommand, CardVerificationResponseDto>
{
    private readonly ICardVerificationService _verificationService;

    /// <summary>
    /// Initializes a new instance of <see cref="CardVerificationHandlers"/>.
    /// </summary>
    public CardVerificationHandlers(ICardVerificationService verificationService)
    {
        _verificationService = verificationService ?? throw new ArgumentNullException(nameof(verificationService));
    }

    /// <inheritdoc/>
    public Task<CardVerificationResponseDto> Handle(
        InitializeCardVerificationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _verificationService.InitializeCardVerificationAsync(
            walletId: request.WalletId,
            userId: request.CurrentUserId,
            email: request.Email,
            callbackUrl: request.CallbackUrl,
            preferredProvider: request.Provider,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CardVerificationResponseDto> Handle(
        CompleteCardVerificationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await _verificationService.CompleteCardVerificationAsync(
            reference: request.Reference,
            cancellationToken: cancellationToken);
    }
}
