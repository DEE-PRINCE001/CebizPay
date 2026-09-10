namespace CebizPay.Application.UseCases.Admin.Analytics;

/// <summary>
/// DTO representing a single month's revenue and volume aggregate data point.
/// </summary>
public sealed record MonthlyDataPointDto(
    string Name,
    decimal Revenue,
    decimal Volume);

/// <summary>
/// DTO representing the yearly platform analytics, fee revenue, and transaction volume time series.
/// </summary>
public sealed record PlatformRevenueAnalyticsDto(
    int Year,
    decimal TotalRevenue,
    decimal TotalTransactionVolume,
    double MomGrowthRate,
    IReadOnlyList<MonthlyDataPointDto> MonthlyData);
