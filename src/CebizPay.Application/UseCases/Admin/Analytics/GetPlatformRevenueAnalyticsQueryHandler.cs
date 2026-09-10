using CebizPay.Application.Common.Interfaces.Analytics;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Analytics;

/// <summary>
/// Handler for <see cref="GetPlatformRevenueAnalyticsQuery"/>.
/// Delegates to <see cref="IPlatformAnalyticsQueryService"/> for database-level grouping and aggregation.
/// </summary>
public sealed class GetPlatformRevenueAnalyticsQueryHandler : IRequestHandler<GetPlatformRevenueAnalyticsQuery, PlatformRevenueAnalyticsDto>
{
    private readonly IPlatformAnalyticsQueryService _analyticsService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetPlatformRevenueAnalyticsQueryHandler"/>.
    /// </summary>
    public GetPlatformRevenueAnalyticsQueryHandler(IPlatformAnalyticsQueryService analyticsService)
    {
        _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
    }

    /// <inheritdoc/>
    public Task<PlatformRevenueAnalyticsDto> Handle(GetPlatformRevenueAnalyticsQuery request, CancellationToken cancellationToken)
    {
        return _analyticsService.GetYearlyRevenueAnalyticsAsync(request.Year, request.Currency, cancellationToken);
    }
}
