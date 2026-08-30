using CebizPay.Domain.Finance.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Wallet.ExternalAccounts;

/// <summary>
/// Response DTO representing an external funding account attached to a wallet.
/// Never exposes internal entity references or credentials.
/// </summary>
public sealed record ExternalFundingAccountResponseDto(
    Guid Id,
    Guid WalletId,
    string Provider,
    string? ProviderCustomerReference,
    string? ProviderAccountReference,
    string AccountNumber,
    string AccountName,
    string BankCode,
    string BankName,
    string Currency,
    string Status,
    bool IsPrimary,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// Query to retrieve all external funding accounts attached to the authenticated user's or organization's wallet.
/// </summary>
public sealed record GetExternalFundingAccountsQuery(
    string CurrentUserId,
    Guid? OrganizationId = null,
    Currency? Currency = null) : IRequest<IReadOnlyList<ExternalFundingAccountResponseDto>>;

/// <summary>
/// Validator for <see cref="GetExternalFundingAccountsQuery"/>.
/// </summary>
public sealed class GetExternalFundingAccountsQueryValidator : AbstractValidator<GetExternalFundingAccountsQuery>
{
    /// <summary>
    /// Initializes validation rules for <see cref="GetExternalFundingAccountsQuery"/>.
    /// </summary>
    public GetExternalFundingAccountsQueryValidator()
    {
        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");

        When(x => x.Currency.HasValue, () =>
        {
            RuleFor(x => x.Currency!.Value)
                .IsInEnum().WithMessage("Currency must be a valid Currency.")
                .Must(c => c.IsTransactionalV1())
                .WithMessage("Currency must be a transactional V1 currency.");
        });
    }
}

/// <summary>
/// Command to designate an external funding account as primary for its parent wallet.
/// Validates tenant ownership and account active status.
/// </summary>
public sealed record SetPrimaryExternalFundingAccountCommand(
    Guid AccountId,
    string CurrentUserId,
    Guid? OrganizationId = null) : IRequest<ExternalFundingAccountResponseDto>;

/// <summary>
/// Validator for <see cref="SetPrimaryExternalFundingAccountCommand"/>.
/// </summary>
public sealed class SetPrimaryExternalFundingAccountCommandValidator : AbstractValidator<SetPrimaryExternalFundingAccountCommand>
{
    /// <summary>
    /// Initializes validation rules for <see cref="SetPrimaryExternalFundingAccountCommand"/>.
    /// </summary>
    public SetPrimaryExternalFundingAccountCommandValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("AccountId is required.");

        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");
    }
}

/// <summary>
/// Query to retrieve a specific external funding account by ID.
/// </summary>
public sealed record GetExternalFundingAccountByIdQuery(
    Guid AccountId,
    string CurrentUserId,
    Guid? OrganizationId = null) : IRequest<ExternalFundingAccountResponseDto?>;

/// <summary>
/// Validator for <see cref="GetExternalFundingAccountByIdQuery"/>.
/// </summary>
public sealed class GetExternalFundingAccountByIdQueryValidator : AbstractValidator<GetExternalFundingAccountByIdQuery>
{
    /// <summary>
    /// Initializes validation rules for <see cref="GetExternalFundingAccountByIdQuery"/>.
    /// </summary>
    public GetExternalFundingAccountByIdQueryValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("AccountId is required.");
        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");
    }
}

/// <summary>
/// Command to provision a new provider-backed Monnify reserved virtual account for the caller's wallet.
/// </summary>
public sealed record ProvisionMonnifyExternalFundingAccountCommand(
    string CurrentUserId,
    Guid? OrganizationId = null,
    Currency Currency = Currency.NGN) : IRequest<ExternalFundingAccountResponseDto>;

/// <summary>
/// Validator for <see cref="ProvisionMonnifyExternalFundingAccountCommand"/>.
/// </summary>
public sealed class ProvisionMonnifyExternalFundingAccountCommandValidator : AbstractValidator<ProvisionMonnifyExternalFundingAccountCommand>
{
    /// <summary>
    /// Initializes validation rules for <see cref="ProvisionMonnifyExternalFundingAccountCommand"/>.
    /// </summary>
    public ProvisionMonnifyExternalFundingAccountCommandValidator()
    {
        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");
        RuleFor(x => x.Currency)
            .IsInEnum().WithMessage("Currency must be a valid Currency.")
            .Must(c => c.IsTransactionalV1())
            .WithMessage("Currency must be a transactional V1 currency.");
    }
}

/// <summary>
/// Command to deactivate / suspend an external funding account.
/// </summary>
public sealed record DeactivateExternalFundingAccountCommand(
    Guid AccountId,
    string CurrentUserId,
    Guid? OrganizationId = null) : IRequest<ExternalFundingAccountResponseDto>;

/// <summary>
/// Validator for <see cref="DeactivateExternalFundingAccountCommand"/>.
/// </summary>
public sealed class DeactivateExternalFundingAccountCommandValidator : AbstractValidator<DeactivateExternalFundingAccountCommand>
{
    /// <summary>
    /// Initializes validation rules for <see cref="DeactivateExternalFundingAccountCommand"/>.
    /// </summary>
    public DeactivateExternalFundingAccountCommandValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("AccountId is required.");
        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");
    }
}

/// <summary>
/// DTO representing a funding transaction details.
/// </summary>
public sealed record FundingTransactionResponseDto(
    Guid Id,
    Guid WalletId,
    Guid? ExternalFundingAccountId,
    string Provider,
    string ProviderTransactionReference,
    string FundingChannel,
    decimal Amount,
    decimal FeeAmount,
    decimal NetCreditedAmount,
    string Currency,
    string Status,
    Guid? LedgerTransactionId,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string? FailureReason);

/// <summary>
/// Query to retrieve a funding transaction by ID.
/// </summary>
public sealed record GetFundingTransactionByIdQuery(
    Guid FundingId,
    string CurrentUserId,
    Guid? OrganizationId = null) : IRequest<FundingTransactionResponseDto?>;

/// <summary>
/// Validator for <see cref="GetFundingTransactionByIdQuery"/>.
/// </summary>
public sealed class GetFundingTransactionByIdQueryValidator : AbstractValidator<GetFundingTransactionByIdQuery>
{
    /// <summary>
    /// Initializes validation rules for <see cref="GetFundingTransactionByIdQuery"/>.
    /// </summary>
    public GetFundingTransactionByIdQueryValidator()
    {
        RuleFor(x => x.FundingId)
            .NotEmpty().WithMessage("FundingId is required.");
        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("CurrentUserId is required.");
    }
}
