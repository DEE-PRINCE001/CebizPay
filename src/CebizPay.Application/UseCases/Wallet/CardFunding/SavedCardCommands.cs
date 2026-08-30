using CebizPay.Application.Common.Interfaces.Payments;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Wallet.CardFunding;

/// <summary>
/// Query to list all active saved cards belonging to the authenticated user.
/// </summary>
public sealed record GetSavedCardsQuery(string CurrentUserId) : IRequest<IReadOnlyList<SavedCardResponseDto>>;

/// <summary>
/// Validator for <see cref="GetSavedCardsQuery"/>.
/// </summary>
public sealed class GetSavedCardsQueryValidator : AbstractValidator<GetSavedCardsQuery>
{
    /// <summary>Initializes validation rules.</summary>
    public GetSavedCardsQueryValidator()
    {
        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");
    }
}

/// <summary>
/// Query to get a specific saved card by ID.
/// </summary>
public sealed record GetSavedCardByIdQuery(Guid CardId, string CurrentUserId) : IRequest<SavedCardResponseDto?>;

/// <summary>
/// Validator for <see cref="GetSavedCardByIdQuery"/>.
/// </summary>
public sealed class GetSavedCardByIdQueryValidator : AbstractValidator<GetSavedCardByIdQuery>
{
    /// <summary>Initializes validation rules.</summary>
    public GetSavedCardByIdQueryValidator()
    {
        RuleFor(x => x.CardId)
            .NotEmpty().WithMessage("CardId is required.");

        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");
    }
}

/// <summary>
/// Command to designate a saved card as the default card.
/// </summary>
public sealed record SetDefaultSavedCardCommand(Guid CardId, string CurrentUserId) : IRequest<SavedCardResponseDto>;

/// <summary>
/// Validator for <see cref="SetDefaultSavedCardCommand"/>.
/// </summary>
public sealed class SetDefaultSavedCardCommandValidator : AbstractValidator<SetDefaultSavedCardCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public SetDefaultSavedCardCommandValidator()
    {
        RuleFor(x => x.CardId)
            .NotEmpty().WithMessage("CardId is required.");

        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");
    }
}

/// <summary>
/// Command to revoke/delete a saved card token.
/// </summary>
public sealed record RevokeSavedCardCommand(Guid CardId, string CurrentUserId) : IRequest<SavedCardResponseDto>;

/// <summary>
/// Validator for <see cref="RevokeSavedCardCommand"/>.
/// </summary>
public sealed class RevokeSavedCardCommandValidator : AbstractValidator<RevokeSavedCardCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public RevokeSavedCardCommandValidator()
    {
        RuleFor(x => x.CardId)
            .NotEmpty().WithMessage("CardId is required.");

        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");
    }
}
