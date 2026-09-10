using CebizPay.Application.UseCases.Admin.Analytics;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.Common.Interfaces.Analytics;

/// <summary>
/// Contract for executing high-performance database-side analytics and time-series aggregation queries.
/// </summary>
public interface IPlatformAnalyticsQueryService
{
    /// <summary>
    /// Computes aggregated revenue, total transaction volume, MoM growth rate, and 12-month time series for a specified year and currency.
    /// </summary>
    Task<PlatformRevenueAnalyticsDto> GetYearlyRevenueAnalyticsAsync(
        int year,
        Currency currency,
        CancellationToken cancellationToken = default);
}
