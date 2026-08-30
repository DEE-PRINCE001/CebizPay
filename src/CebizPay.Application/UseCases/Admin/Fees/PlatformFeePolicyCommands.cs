using CebizPay.Domain.Finance.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Fees;

/// <summary>
/// Response DTO for a platform fee policy.
/// </summary>
public sealed record PlatformFeePolicyResponseDto(
    Guid Id,
    string OperationType,
    string CalculationMethod,
    string FeeBearer,
    decimal? FixedAmount,
    decimal? PercentageRate,
    decimal? MinimumFee,
    decimal? MaximumFee,
    string Currency,
    bool IsEnabled,
    int Version,
    string CreatedByUserId,
    DateTime EffectiveFromUtc,
    DateTime CreatedAtUtc,
    DateTime? DeactivatedAtUtc);

/// <summary>
/// Command to create and activate a new generalized platform fee policy.
/// Super Admin only — permission Fees.ManagePlatformPolicy is required.
/// </summary>
public sealed record CreatePlatformFeePolicyCommand(
    FeeOperationType OperationType,
    FeeCalculationMethod CalculationMethod,
    FeeBearer FeeBearer,
    decimal? FixedAmount,
    decimal? PercentageRate,
    decimal? MinimumFee,
    decimal? MaximumFee,
    Currency Currency,
    string CreatedByUserId,
    DateTime? EffectiveFromUtc = null) : IRequest<PlatformFeePolicyResponseDto>;

/// <summary>
/// Validator for <see cref="CreatePlatformFeePolicyCommand"/>.
/// </summary>
public sealed class CreatePlatformFeePolicyCommandValidator : AbstractValidator<CreatePlatformFeePolicyCommand>
{
    /// <summary>
    /// Initializes validation rules for <see cref="CreatePlatformFeePolicyCommand"/>.
    /// </summary>
    public CreatePlatformFeePolicyCommandValidator()
    {
        RuleFor(x => x.CreatedByUserId)
            .NotEmpty().WithMessage("CreatedByUserId is required.");

        RuleFor(x => x.OperationType)
            .IsInEnum().WithMessage("OperationType must be a valid FeeOperationType.");

        RuleFor(x => x.CalculationMethod)
            .IsInEnum().WithMessage("CalculationMethod must be a valid FeeCalculationMethod.");

        RuleFor(x => x.FeeBearer)
            .IsInEnum().WithMessage("FeeBearer must be a valid FeeBearer.");

        RuleFor(x => x.Currency)
            .IsInEnum().WithMessage("Currency must be a valid Currency.")
            .Must(c => c.IsTransactionalV1())
            .WithMessage("Currency must be a transactional V1 currency (NGN, INTERNATIONAL_NGN, USDT).");

        When(x => x.CalculationMethod == FeeCalculationMethod.Fixed, () =>
        {
            RuleFor(x => x.FixedAmount)
                .NotNull().WithMessage("FixedAmount is required for Fixed calculation method.")
                .GreaterThanOrEqualTo(0).WithMessage("FixedAmount must be >= 0.");
        });

        When(x => x.CalculationMethod == FeeCalculationMethod.Percentage, () =>
        {
            RuleFor(x => x.PercentageRate)
                .NotNull().WithMessage("PercentageRate is required for Percentage calculation method.")
                .GreaterThan(0).WithMessage("PercentageRate must be greater than 0.");
        });

        When(x => x.CalculationMethod == FeeCalculationMethod.PercentageWithCap, () =>
        {
            RuleFor(x => x.PercentageRate)
                .NotNull().WithMessage("PercentageRate is required for PercentageWithCap calculation method.")
                .GreaterThan(0).WithMessage("PercentageRate must be greater than 0.");

            When(x => x.MinimumFee.HasValue, () =>
            {
                RuleFor(x => x.MinimumFee!.Value)
                    .GreaterThanOrEqualTo(0).WithMessage("MinimumFee cannot be negative.");
            });

            When(x => x.MaximumFee.HasValue, () =>
            {
                RuleFor(x => x.MaximumFee!.Value)
                    .GreaterThanOrEqualTo(0).WithMessage("MaximumFee cannot be negative.");
            });

            When(x => x.MinimumFee.HasValue && x.MaximumFee.HasValue, () =>
            {
                RuleFor(x => x.MaximumFee!.Value)
                    .GreaterThanOrEqualTo(x => x.MinimumFee!.Value)
                    .WithMessage("MaximumFee must be greater than or equal to MinimumFee.");
            });
        });
    }
}

/// <summary>
/// Query to retrieve all historical and current platform fee policies, optionally filtered by operation type.
/// </summary>
public sealed record GetAllPlatformFeePoliciesQuery(
    FeeOperationType? OperationType = null) : IRequest<IReadOnlyList<PlatformFeePolicyResponseDto>>;

/// <summary>
/// Query to retrieve the currently active platform fee policy for a specific operation type.
/// </summary>
public sealed record GetActivePlatformFeePolicyQuery(
    FeeOperationType OperationType) : IRequest<PlatformFeePolicyResponseDto?>;
