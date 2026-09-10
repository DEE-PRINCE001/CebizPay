namespace CebizPay.Application.UseCases.Admin.Treasury;

/// <summary>
/// DTO representing the platform master wallet and treasury liquidity summary.
/// </summary>
public sealed record PlatformTreasurySummaryDto(
    string Currency,
    string Symbol,
    decimal AvailableBalance,
    decimal LedgerBalance,
    decimal PendingSettlement,
    DateTime? LastReconciliationAt);
