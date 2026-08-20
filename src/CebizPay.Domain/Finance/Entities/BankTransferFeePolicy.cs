using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Finance.Entities;

/// <summary>
/// Domain entity representing a versioned platform bank-transfer fee policy.
/// Only one policy may be active at a time. Historical policies are preserved immutably.
/// Rounding strategy: MidpointRounding.AwayFromZero to currency decimals (2 for V1 currencies).
/// </summary>
public class BankTransferFeePolicy
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Fee policy mode: FREE or PERCENTAGE.</summary>
    public FeePolicyMode Mode { get; private set; }

    /// <summary>
    /// Decimal percentage rate (e.g. 0.015 = 1.5%).
    /// Required when Mode == PERCENTAGE. Ignored for FREE.
    /// </summary>
    public decimal? PercentageRate { get; private set; }

    /// <summary>
    /// Minimum fee amount applied after percentage calculation.
    /// Required when Mode == PERCENTAGE. Ignored for FREE.
    /// </summary>
    public decimal? MinimumFee { get; private set; }

    /// <summary>
    /// Maximum fee amount applied after percentage calculation.
    /// Required when Mode == PERCENTAGE. Must be >= MinimumFee.
    /// </summary>
    public decimal? MaximumFee { get; private set; }

    /// <summary>Whether this policy is the currently active policy.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Timestamp from which this policy took effect.</summary>
    public DateTime EffectiveFrom { get; private set; }

    /// <summary>Auto-incrementing policy version number for historical traceability.</summary>
    public int Version { get; private set; }

    /// <summary>UserId of the Super Admin who created this policy.</summary>
    public string CreatedByUserId { get; private set; } = string.Empty;

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Timestamp when this policy was deactivated, if applicable.</summary>
    public DateTime? DeactivatedAtUtc { get; private set; }

    private BankTransferFeePolicy() { } // EF Core

    /// <summary>
    /// Creates a new bank-transfer fee policy with invariant validation.
    /// </summary>
    /// <param name="mode">FREE or PERCENTAGE.</param>
    /// <param name="percentageRate">Required for PERCENTAGE mode. Must be > 0.</param>
    /// <param name="minimumFee">Required for PERCENTAGE mode. Must be >= 0.</param>
    /// <param name="maximumFee">Required for PERCENTAGE mode. Must be >= minimumFee.</param>
    /// <param name="version">Policy version number (caller-assigned, must be positive).</param>
    /// <param name="createdByUserId">Super Admin UserId creating this policy.</param>
    /// <param name="effectiveFrom">UTC timestamp from which this policy is effective.</param>
    public static BankTransferFeePolicy Create(
        FeePolicyMode mode,
        decimal? percentageRate,
        decimal? minimumFee,
        decimal? maximumFee,
        int version,
        string createdByUserId,
        DateTime effectiveFrom)
    {
        if (string.IsNullOrWhiteSpace(createdByUserId))
            throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        if (version <= 0)
            throw new ArgumentException("Version must be a positive integer.", nameof(version));

        if (mode == FeePolicyMode.Percentage)
        {
            if (!percentageRate.HasValue || percentageRate.Value <= 0)
                throw new ArgumentException("PercentageRate must be greater than 0 for PERCENTAGE mode.", nameof(percentageRate));
            if (!minimumFee.HasValue || minimumFee.Value < 0)
                throw new ArgumentException("MinimumFee must be >= 0 for PERCENTAGE mode.", nameof(minimumFee));
            if (!maximumFee.HasValue || maximumFee.Value < minimumFee.Value)
                throw new ArgumentException("MaximumFee must be >= MinimumFee for PERCENTAGE mode.", nameof(maximumFee));
        }

        return new BankTransferFeePolicy
        {
            Id = Guid.NewGuid(),
            Mode = mode,
            PercentageRate = mode == FeePolicyMode.Percentage ? percentageRate : null,
            MinimumFee = mode == FeePolicyMode.Percentage ? minimumFee : null,
            MaximumFee = mode == FeePolicyMode.Percentage ? maximumFee : null,
            IsEnabled = true,
            EffectiveFrom = effectiveFrom,
            Version = version,
            CreatedByUserId = createdByUserId.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Calculates the fee for a given transfer amount based on this policy.
    /// Rounding: MidpointRounding.AwayFromZero to currency decimal places (2 for V1 currencies: NGN, INTERNATIONAL_NGN, USDT).
    /// Never returns a negative value.
    /// </summary>
    /// <param name="transferAmount">The gross transfer amount (must be > 0).</param>
    /// <param name="currency">The V1 transactional currency.</param>
    /// <returns>The calculated fee amount.</returns>
    public decimal CalculateFee(decimal transferAmount, Currency currency = Currency.NGN)
    {
        if (!IsEnabled)
            throw new InvalidOperationException("Cannot calculate fee using an inactive fee policy.");
        if (transferAmount <= 0)
            throw new ArgumentException("Transfer amount must be positive.", nameof(transferAmount));

        if (Mode == FeePolicyMode.Free)
            return 0m;

        // PERCENTAGE mode
        var decimals = currency.GetDecimalPlaces();
        var rawFee = transferAmount * PercentageRate!.Value;
        var roundedFee = decimal.Round(rawFee, decimals, MidpointRounding.AwayFromZero);
        var clampedFee = Math.Max(MinimumFee!.Value, Math.Min(MaximumFee!.Value, roundedFee));
        return Math.Max(0m, clampedFee);
    }

    /// <summary>
    /// Deactivates this policy. Called when a newer policy supersedes it.
    /// </summary>
    public void Deactivate()
    {
        IsEnabled = false;
        DeactivatedAtUtc = DateTime.UtcNow;
    }
}
