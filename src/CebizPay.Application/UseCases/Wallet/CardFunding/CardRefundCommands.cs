using CebizPay.Application.Common.Interfaces.Payments;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Wallet.CardFunding;

/// <summary>
/// Command to request a refund for a completed card funding transaction.
/// </summary>
public sealed record RequestCardRefundCommand(
    Guid FundingTransactionId,
    decimal Amount,
    string Reason,
    string IdempotencyKey,
    string CurrentUserId) : IRequest<CardRefundResponseDto>;

/// <summary>
/// Validator for <see cref="RequestCardRefundCommand"/>.
/// </summary>
public sealed class RequestCardRefundCommandValidator : AbstractValidator<RequestCardRefundCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public RequestCardRefundCommandValidator()
    {
        RuleFor(x => x.FundingTransactionId)
            .NotEmpty().WithMessage("FundingTransactionId is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Refund amount must be greater than zero.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("IdempotencyKey is required.");

        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");
    }
}

/// <summary>
/// Query to retrieve a card refund by ID.
/// </summary>
public sealed record GetCardRefundByIdQuery(Guid RefundId, string CurrentUserId) : IRequest<CardRefundResponseDto?>;

/// <summary>
/// Validator for <see cref="GetCardRefundByIdQuery"/>.
/// </summary>
public sealed class GetCardRefundByIdQueryValidator : AbstractValidator<GetCardRefundByIdQuery>
{
    /// <summary>Initializes validation rules.</summary>
    public GetCardRefundByIdQueryValidator()
    {
        RuleFor(x => x.RefundId)
            .NotEmpty().WithMessage("RefundId is required.");

        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");
    }
}

/// <summary>
/// Command to reconcile a pending card refund status against the provider gateway.
/// </summary>
public sealed record ReconcileCardRefundCommand(Guid RefundId, string CurrentUserId) : IRequest<CardRefundResponseDto>;

/// <summary>
/// Validator for <see cref="ReconcileCardRefundCommand"/>.
/// </summary>
public sealed class ReconcileCardRefundCommandValidator : AbstractValidator<ReconcileCardRefundCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public ReconcileCardRefundCommandValidator()
    {
        RuleFor(x => x.RefundId)
            .NotEmpty().WithMessage("RefundId is required.");

        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");
    }
}
