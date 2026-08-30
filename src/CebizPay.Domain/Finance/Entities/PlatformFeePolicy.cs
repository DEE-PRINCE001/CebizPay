using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Finance.Models;

namespace CebizPay.Domain.Finance.Entities;

/// <summary>
/// Domain aggregate entity representing a versioned, configurable platform fee policy.
/// Supports FREE, FIXED, PERCENTAGE, and PERCENTAGE_WITH_CAP calculation methods,
/// along with CUSTOMER_PAYS, DEDUCT_FROM_FUNDS, and PLATFORM_ABSORBS bearer settlement models.
/// Historical versions remain immutable.
/// </summary>
public class PlatformFeePolicy
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The financial operation type governed by this policy.</summary>
    public FeeOperationType OperationType { get; private set; }

    /// <summary>Mathematical calculation method applied.</summary>
    public FeeCalculationMethod CalculationMethod { get; private set; }

    /// <summary>Specifies which party bears the platform fee.</summary>
    public FeeBearer FeeBearer { get; private set; }

    /// <summary>Fixed fee amount (required when CalculationMethod == Fixed).</summary>
    public decimal? FixedAmount { get; private set; }

    /// <summary>Percentage rate, e.g. 1.5 for 1.5% (required for Percentage and PercentageWithCap).</summary>
    public decimal? PercentageRate { get; private set; }

    /// <summary>Minimum fee floor applied after percentage calculation (for PercentageWithCap).</summary>
    public decimal? MinimumFee { get; private set; }

    /// <summary>Maximum fee ceiling applied after percentage calculation (for PercentageWithCap).</summary>
    public decimal? MaximumFee { get; private set; }

    /// <summary>Transactional currency this policy applies to.</summary>
    public Currency Currency { get; private set; }

    /// <summary>Auto-incrementing version number per operation type.</summary>
    public int Version { get; private set; }

    /// <summary>Whether this policy is currently active.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>UTC timestamp from which this policy took effect.</summary>
    public DateTime EffectiveFromUtc { get; private set; }

    /// <summary>UTC timestamp when this policy was superseded/deactivated.</summary>
    public DateTime? DeactivatedAtUtc { get; private set; }

    /// <summary>Super Admin UserId who authored/activated this policy version.</summary>
    public string CreatedByUserId { get; private set; } = string.Empty;

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last updated timestamp (UTC).</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    private PlatformFeePolicy() { } // EF Core

    /// <summary>
    /// Creates a new versioned PlatformFeePolicy with strict domain invariant validation.
    /// </summary>
    public static PlatformFeePolicy Create(
        FeeOperationType operationType,
        FeeCalculationMethod calculationMethod,
        FeeBearer feeBearer,
        decimal? fixedAmount,
        decimal? percentageRate,
        decimal? minimumFee,
        decimal? maximumFee,
        Currency currency,
        int version,
        string createdByUserId,
        DateTime effectiveFromUtc)
    {
        if (string.IsNullOrWhiteSpace(createdByUserId))
            throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        if (version <= 0)
            throw new ArgumentException("Version must be a positive integer.", nameof(version));

        currency.EnsureTransactionalV1();

        ValidateCalculationParameters(calculationMethod, fixedAmount, percentageRate, minimumFee, maximumFee);

        return new PlatformFeePolicy
        {
            Id = Guid.NewGuid(),
            OperationType = operationType,
            CalculationMethod = calculationMethod,
            FeeBearer = feeBearer,
            FixedAmount = calculationMethod == FeeCalculationMethod.Fixed ? fixedAmount : null,
            PercentageRate = (calculationMethod is FeeCalculationMethod.Percentage or FeeCalculationMethod.PercentageWithCap) ? percentageRate : null,
            MinimumFee = calculationMethod == FeeCalculationMethod.PercentageWithCap ? minimumFee : null,
            MaximumFee = calculationMethod == FeeCalculationMethod.PercentageWithCap ? maximumFee : null,
            Currency = currency,
            Version = version,
            IsEnabled = true,
            EffectiveFromUtc = effectiveFromUtc,
            CreatedByUserId = createdByUserId.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new free fee policy (zero fee).
    /// </summary>
    public static PlatformFeePolicy CreateFree(
        FeeOperationType operationType,
        FeeBearer feeBearer,
        Currency currency,
        int version,
        string createdByUserId,
        DateTime? effectiveFromUtc = null) =>
        Create(operationType, FeeCalculationMethod.Free, feeBearer, null, null, null, null, currency, version, createdByUserId, effectiveFromUtc ?? DateTime.UtcNow);

    /// <summary>
    /// Creates a new fixed-amount fee policy.
    /// </summary>
    public static PlatformFeePolicy CreateFixed(
        FeeOperationType operationType,
        decimal fixedAmount,
        FeeBearer feeBearer,
        Currency currency,
        int version,
        string createdByUserId,
        DateTime? effectiveFromUtc = null) =>
        Create(operationType, FeeCalculationMethod.Fixed, feeBearer, fixedAmount, null, null, null, currency, version, createdByUserId, effectiveFromUtc ?? DateTime.UtcNow);

    /// <summary>
    /// Creates a new percentage-rate fee policy.
    /// </summary>
    public static PlatformFeePolicy CreatePercentage(
        FeeOperationType operationType,
        decimal percentageRate,
        FeeBearer feeBearer,
        Currency currency,
        int version,
        string createdByUserId,
        DateTime? effectiveFromUtc = null) =>
        Create(operationType, FeeCalculationMethod.Percentage, feeBearer, null, percentageRate, null, null, currency, version, createdByUserId, effectiveFromUtc ?? DateTime.UtcNow);

    /// <summary>
    /// Creates a new percentage-rate fee policy with min floor and max cap.
    /// </summary>
    public static PlatformFeePolicy CreatePercentageWithCap(
        FeeOperationType operationType,
        decimal percentageRate,
        decimal minimumFee,
        decimal maximumFee,
        FeeBearer feeBearer,
        Currency currency,
        int version,
        string createdByUserId,
        DateTime? effectiveFromUtc = null) =>
        Create(operationType, FeeCalculationMethod.PercentageWithCap, feeBearer, null, percentageRate, minimumFee, maximumFee, currency, version, createdByUserId, effectiveFromUtc ?? DateTime.UtcNow);

    private static void ValidateCalculationParameters(
        FeeCalculationMethod method,
        decimal? fixedAmount,
        decimal? percentageRate,
        decimal? minimumFee,
        decimal? maximumFee)
    {
        switch (method)
        {
            case FeeCalculationMethod.Free:
                break;

            case FeeCalculationMethod.Fixed:
                if (!fixedAmount.HasValue)
                    throw new ArgumentException("FixedAmount is required for FIXED calculation method.", nameof(fixedAmount));
                if (fixedAmount.Value < 0)
                    throw new ArgumentException("FixedAmount cannot be negative.", nameof(fixedAmount));
                break;

            case FeeCalculationMethod.Percentage:
                if (!percentageRate.HasValue)
                    throw new ArgumentException("PercentageRate is required for PERCENTAGE calculation method.", nameof(percentageRate));
                if (percentageRate.Value <= 0)
                    throw new ArgumentException("PercentageRate must be greater than 0.", nameof(percentageRate));
                break;

            case FeeCalculationMethod.PercentageWithCap:
                if (!percentageRate.HasValue)
                    throw new ArgumentException("PercentageRate is required for PERCENTAGE_WITH_CAP calculation method.", nameof(percentageRate));
                if (percentageRate.Value <= 0)
                    throw new ArgumentException("PercentageRate must be greater than 0.", nameof(percentageRate));
                if (minimumFee.HasValue && minimumFee.Value < 0)
                    throw new ArgumentException("MinimumFee cannot be negative.", nameof(minimumFee));
                if (maximumFee.HasValue && minimumFee.HasValue && maximumFee.Value < minimumFee.Value)
                    throw new ArgumentException("MaximumFee cannot be less than MinimumFee.", nameof(maximumFee));
                if (maximumFee.HasValue && maximumFee.Value < 0)
                    throw new ArgumentException("MaximumFee cannot be negative.", nameof(maximumFee));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(method), $"Unsupported calculation method: {method}");
        }
    }

    /// <summary>
    /// Calculates the platform fee for the specified transactional amount using decimal arithmetic only.
    /// Rounding: MidpointRounding.AwayFromZero to currency precision.
    /// </summary>
    public decimal CalculateFee(decimal amount, Currency? currency = null)
    {
        if (!IsEnabled)
            throw new InvalidOperationException("Cannot calculate fee using a deactivated fee policy.");
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));

        var targetCurrency = currency ?? Currency;
        targetCurrency.EnsureTransactionalV1();
        var decimals = targetCurrency.GetDecimalPlaces();

        return CalculationMethod switch
        {
            FeeCalculationMethod.Free => 0m,

            FeeCalculationMethod.Fixed => decimal.Round(FixedAmount!.Value, decimals, MidpointRounding.AwayFromZero),

            FeeCalculationMethod.Percentage =>
                decimal.Round(amount * (PercentageRate!.Value / 100m), decimals, MidpointRounding.AwayFromZero),

            FeeCalculationMethod.PercentageWithCap =>
                CalculatePercentageWithCap(amount, decimals),

            _ => throw new InvalidOperationException($"Unsupported calculation method: {CalculationMethod}")
        };
    }

    private decimal CalculatePercentageWithCap(decimal amount, int decimals)
    {
        var rawFee = amount * (PercentageRate!.Value / 100m);
        var roundedFee = decimal.Round(rawFee, decimals, MidpointRounding.AwayFromZero);
        var clampedFee = roundedFee;

        if (MinimumFee.HasValue)
        {
            clampedFee = Math.Max(MinimumFee.Value, clampedFee);
        }

        if (MaximumFee.HasValue)
        {
            clampedFee = Math.Min(MaximumFee.Value, clampedFee);
        }

        return Math.Max(0m, clampedFee);
    }

    /// <summary>
    /// Calculates the full settlement breakdown including customer charge, beneficiary net credit, and platform cost.
    /// </summary>
    public FeeBreakdown CalculateBreakdown(decimal amount, Currency? currency = null)
    {
        var fee = CalculateFee(amount, currency);

        decimal totalCustomerCharge;
        decimal netBeneficiaryCredit;
        decimal platformFeeCost;

        switch (FeeBearer)
        {
            case FeeBearer.CustomerPays:
                totalCustomerCharge = amount + fee;
                netBeneficiaryCredit = amount;
                platformFeeCost = 0m;
                break;

            case FeeBearer.DeductFromFunds:
                totalCustomerCharge = amount;
                netBeneficiaryCredit = Math.Max(0m, amount - fee);
                platformFeeCost = 0m;
                break;

            case FeeBearer.PlatformAbsorbs:
                totalCustomerCharge = amount;
                netBeneficiaryCredit = amount;
                platformFeeCost = fee;
                break;

            default:
                throw new InvalidOperationException($"Unsupported fee bearer: {FeeBearer}");
        }

        return new FeeBreakdown(
            Amount: amount,
            Fee: fee,
            FeeBearer: FeeBearer,
            TotalCustomerCharge: totalCustomerCharge,
            NetBeneficiaryCredit: netBeneficiaryCredit,
            PlatformFeeCost: platformFeeCost);
    }

    /// <summary>
    /// Deactivates this policy version.
    /// </summary>
    public void Deactivate()
    {
        IsEnabled = false;
        DeactivatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
