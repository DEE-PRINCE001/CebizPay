using MediatR;

namespace CebizPay.Application.UseCases.Admin.Dashboard;

/// <summary>
/// Query to retrieve aggregated platform KPI metrics summary for platform admins.
/// </summary>
public sealed record GetPlatformAdminMetricsQuery : IRequest<PlatformAdminMetricsDto>;
