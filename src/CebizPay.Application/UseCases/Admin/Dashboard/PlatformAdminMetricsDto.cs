namespace CebizPay.Application.UseCases.Admin.Dashboard;

/// <summary>
/// DTO representing the platform-wide administrative KPI metrics summary.
/// </summary>
public sealed record PlatformAdminMetricsDto(
    int TotalOrganizations,
    int TotalIndividuals,
    int PendingKycUsers,
    int ActiveUsers,
    int RejectedKycUsers,
    int ActiveSavingPlans,
    DateTime Timestamp);
