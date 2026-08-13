using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Finance.Entities;

/// <summary>
/// Domain entity representing explicit cross-currency FX conversion relationships.
/// Rules: SourceCurrency != TargetCurrency, SourceAmount > 0, TargetAmount > 0, Rate > 0.
/// </summary>
public class FxConversion
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Associated LedgerTransaction ID.</summary>
    public Guid LedgerTransactionId { get; private set; }

    /// <summary>Source currency code.</summary>
    public Currency SourceCurrency { get; private set; }

    /// <summary>Target currency code.</summary>
    public Currency TargetCurrency { get; private set; }

    /// <summary>Source monetary amount.</summary>
    public decimal SourceAmount { get; private set; }

    /// <summary>Target monetary amount.</summary>
    public decimal TargetAmount { get; private set; }

    /// <summary>Applied conversion rate (TargetAmount = SourceAmount * Rate).</summary>
    public decimal Rate { get; private set; }

    /// <summary>FX rate provider identifier.</summary>
    public string RateProvider { get; private set; } = string.Empty;

    /// <summary>Rate timestamp.</summary>
    public DateTime RateTimestamp { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private FxConversion() { } // EF Core

    /// <summary>
    /// Creates a new FX conversion record.
    /// </summary>
    public FxConversion(
        Guid ledgerTransactionId,
        Currency sourceCurrency,
        Currency targetCurrency,
        decimal sourceAmount,
        decimal targetAmount,
        decimal rate,
        string rateProvider,
        DateTime rateTimestamp)
    {
        if (ledgerTransactionId == Guid.Empty)
            throw new ArgumentException("LedgerTransactionId is required.", nameof(ledgerTransactionId));
        if (sourceCurrency == targetCurrency)
            throw new ArgumentException("SourceCurrency and TargetCurrency must be different for FX conversion.", nameof(targetCurrency));
        if (sourceAmount <= 0)
            throw new ArgumentException("SourceAmount must be positive.", nameof(sourceAmount));
        if (targetAmount <= 0)
            throw new ArgumentException("TargetAmount must be positive.", nameof(targetAmount));
        if (rate <= 0)
            throw new ArgumentException("Rate must be positive.", nameof(rate));
        if (string.IsNullOrWhiteSpace(rateProvider))
            throw new ArgumentException("RateProvider is required.", nameof(rateProvider));

        Id = Guid.NewGuid();
        LedgerTransactionId = ledgerTransactionId;
        SourceCurrency = sourceCurrency;
        TargetCurrency = targetCurrency;
        SourceAmount = sourceAmount;
        TargetAmount = targetAmount;
        Rate = rate;
        RateProvider = rateProvider.Trim();
        RateTimestamp = rateTimestamp;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
