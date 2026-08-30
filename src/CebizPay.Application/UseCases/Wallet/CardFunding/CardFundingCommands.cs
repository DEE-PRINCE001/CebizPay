using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Wallet.CardFunding;

/// <summary>
/// Command to initialize a hosted card funding session.
/// </summary>
public sealed record InitializeCardFundingCommand(
    Guid WalletId,
    decimal Amount,
    Currency Currency,
    PaymentProvider? Provider,
    string CallbackUrl,
    string CurrentUserId) : IRequest<CardFundingInitializationResponse>;

/// <summary>
/// Validator for <see cref="InitializeCardFundingCommand"/>.
/// </summary>
public sealed class InitializeCardFundingCommandValidator : AbstractValidator<InitializeCardFundingCommand>
{
    /// <summary>
    /// Initializes validation rules.
    /// </summary>
    public InitializeCardFundingCommandValidator()
    {
        RuleFor(x => x.WalletId)
            .NotEmpty().WithMessage("WalletId is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.Currency)
            .IsInEnum().WithMessage("Currency must be a valid Currency.")
            .Must(c => c.IsTransactionalV1()).WithMessage("Currency must be a transactional V1 currency.");

        RuleFor(x => x.CallbackUrl)
            .NotEmpty().WithMessage("CallbackUrl is required.");

        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");
    }
}

/// <summary>
/// Command to charge an existing tokenized saved card.
/// </summary>
public sealed record ChargeSavedCardCommand(
    Guid SavedCardId,
    decimal Amount,
    Currency Currency,
    string IdempotencyKey,
    string CurrentUserId) : IRequest<ChargeSavedCardResponseDto>;

/// <summary>
/// Validator for <see cref="ChargeSavedCardCommand"/>.
/// </summary>
public sealed class ChargeSavedCardCommandValidator : AbstractValidator<ChargeSavedCardCommand>
{
    /// <summary>
    /// Initializes validation rules.
    /// </summary>
    public ChargeSavedCardCommandValidator()
    {
        RuleFor(x => x.SavedCardId)
            .NotEmpty().WithMessage("SavedCardId is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.Currency)
            .IsInEnum().WithMessage("Currency must be a valid Currency.")
            .Must(c => c.IsTransactionalV1()).WithMessage("Currency must be a transactional V1 currency.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("IdempotencyKey is required.");

        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");
    }
}

/// <summary>
/// Command to reconcile an in-flight card funding transaction with the provider gateway.
/// </summary>
public sealed record ReconcileCardFundingCommand(
    Guid FundingTransactionId,
    string CurrentUserId) : IRequest<PaymentProviderResult>;

/// <summary>
/// Validator for <see cref="ReconcileCardFundingCommand"/>.
/// </summary>
public sealed class ReconcileCardFundingCommandValidator : AbstractValidator<ReconcileCardFundingCommand>
{
    /// <summary>
    /// Initializes validation rules.
    /// </summary>
    public ReconcileCardFundingCommandValidator()
    {
        RuleFor(x => x.FundingTransactionId)
            .NotEmpty().WithMessage("FundingTransactionId is required.");

        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");
    }
}
