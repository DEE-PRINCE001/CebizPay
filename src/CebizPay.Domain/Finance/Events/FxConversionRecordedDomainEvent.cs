using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Finance.Events;

/// <summary>
/// Domain event emitted when an FX conversion is recorded.
/// </summary>
/// <param name="ConversionId">FX conversion record ID.</param>
/// <param name="LedgerTransactionId">Associated ledger transaction ID.</param>
/// <param name="SourceCurrency">Source currency.</param>
/// <param name="TargetCurrency">Target currency.</param>
/// <param name="SourceAmount">Source amount.</param>
/// <param name="TargetAmount">Target amount.</param>
/// <param name="Rate">Conversion rate applied.</param>
/// <param name="OccurredOnUtc">Timestamp when event occurred.</param>
public sealed record FxConversionRecordedDomainEvent(
    Guid ConversionId,
    Guid LedgerTransactionId,
    Currency SourceCurrency,
    Currency TargetCurrency,
    decimal SourceAmount,
    decimal TargetAmount,
    decimal Rate,
    DateTime OccurredOnUtc);
