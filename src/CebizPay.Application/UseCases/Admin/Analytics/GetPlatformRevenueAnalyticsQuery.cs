using CebizPay.Domain.Finance.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Analytics;

/// <summary>
/// Query to retrieve yearly platform revenue, transaction volume, and monthly time-series analytics.
/// </summary>
public sealed record GetPlatformRevenueAnalyticsQuery(
    int Year,
    Currency Currency = Currency.NGN) : IRequest<PlatformRevenueAnalyticsDto>;
