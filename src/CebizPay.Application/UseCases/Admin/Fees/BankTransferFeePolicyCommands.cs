using CebizPay.Domain.Finance.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Fees;

/// <summary>
/// Response DTO for a platform bank-transfer fee policy.
/// </summary>
public sealed record BankTransferFeePolicyResponseDto(
    Guid Id,
    string Mode,
    decimal? PercentageRate,
    decimal? MinimumFee,
    decimal? MaximumFee,
    bool IsEnabled,
    int Version,
    string CreatedByUserId,
    DateTime EffectiveFrom,
    DateTime CreatedAtUtc,
    DateTime? DeactivatedAtUtc);

/// <summary>
/// Command to create and activate a new platform bank-transfer fee policy.
/// Super Admin only — permission Fees.ManageBankTransferPolicy is required.
/// </summary>
public sealed record CreateBankTransferFeePolicyCommand(
    FeePolicyMode Mode,
    decimal? PercentageRate,
    decimal? MinimumFee,
    decimal? MaximumFee,
    string CreatedByUserId) : IRequest<BankTransferFeePolicyResponseDto>;

/// <summary>
/// Validator for <see cref="CreateBankTransferFeePolicyCommand"/>.
/// </summary>
public sealed class CreateBankTransferFeePolicyCommandValidator : AbstractValidator<CreateBankTransferFeePolicyCommand>
{
    /// <summary>
    /// Initializes validation rules for <see cref="CreateBankTransferFeePolicyCommand"/>.
    /// </summary>
    public CreateBankTransferFeePolicyCommandValidator()
    {
        RuleFor(x => x.CreatedByUserId)
            .NotEmpty().WithMessage("CreatedByUserId is required.");

        RuleFor(x => x.Mode)
            .IsInEnum().WithMessage("Mode must be a valid FeePolicyMode (Free = 1, Percentage = 2).");

        When(x => x.Mode == FeePolicyMode.Percentage, () =>
        {
            RuleFor(x => x.PercentageRate)
                .NotNull().WithMessage("PercentageRate is required for Percentage mode.")
                .GreaterThan(0).WithMessage("PercentageRate must be greater than 0.");

            RuleFor(x => x.MinimumFee)
                .NotNull().WithMessage("MinimumFee is required for Percentage mode.")
                .GreaterThanOrEqualTo(0).WithMessage("MinimumFee must be >= 0.");

            RuleFor(x => x.MaximumFee)
                .NotNull().WithMessage("MaximumFee is required for Percentage mode.")
                .Must((cmd, max) => max >= cmd.MinimumFee)
                .WithMessage("MaximumFee must be greater than or equal to MinimumFee.");
        });
    }
}

/// <summary>
/// Query to retrieve all historical and current bank-transfer fee policies.
/// </summary>
public sealed record GetAllBankTransferFeePoliciesQuery : IRequest<IReadOnlyList<BankTransferFeePolicyResponseDto>>;

/// <summary>
/// Query to retrieve the currently active bank-transfer fee policy.
/// </summary>
public sealed record GetActiveBankTransferFeePolicyQuery : IRequest<BankTransferFeePolicyResponseDto?>;
