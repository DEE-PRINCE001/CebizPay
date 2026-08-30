using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Payments.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Wallet.CardFunding;

/// <summary>
/// Command to initialize a card verification session (zero-auth or micro-charge).
/// </summary>
public sealed record InitializeCardVerificationCommand(
    Guid WalletId,
    string Email,
    string CallbackUrl,
    PaymentProvider? Provider,
    string CurrentUserId) : IRequest<CardVerificationResponseDto>;

/// <summary>
/// Validator for <see cref="InitializeCardVerificationCommand"/>.
/// </summary>
public sealed class InitializeCardVerificationCommandValidator : AbstractValidator<InitializeCardVerificationCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public InitializeCardVerificationCommandValidator()
    {
        RuleFor(x => x.WalletId)
            .NotEmpty().WithMessage("WalletId is required.");

        RuleFor(x => x.Email)
            .NotEmpty().EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.CallbackUrl)
            .NotEmpty().WithMessage("CallbackUrl is required.");

        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");
    }
}

/// <summary>
/// Command to complete card verification and save verified token.
/// </summary>
public sealed record CompleteCardVerificationCommand(
    string Reference,
    string CurrentUserId) : IRequest<CardVerificationResponseDto>;

/// <summary>
/// Validator for <see cref="CompleteCardVerificationCommand"/>.
/// </summary>
public sealed class CompleteCardVerificationCommandValidator : AbstractValidator<CompleteCardVerificationCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public CompleteCardVerificationCommandValidator()
    {
        RuleFor(x => x.Reference)
            .NotEmpty().WithMessage("Reference is required.");

        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");
    }
}
