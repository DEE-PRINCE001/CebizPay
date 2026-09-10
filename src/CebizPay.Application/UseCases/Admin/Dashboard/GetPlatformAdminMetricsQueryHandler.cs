using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Savings.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Dashboard;

/// <summary>
/// Handler for <see cref="GetPlatformAdminMetricsQuery"/>.
/// Aggregates primary platform KPIs in fast, indexed queries.
/// </summary>
public sealed class GetPlatformAdminMetricsQueryHandler : IRequestHandler<GetPlatformAdminMetricsQuery, PlatformAdminMetricsDto>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetPlatformAdminMetricsQueryHandler"/>.
    /// </summary>
    public GetPlatformAdminMetricsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<PlatformAdminMetricsDto> Handle(GetPlatformAdminMetricsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var totalOrganizations = await _dbContext.Organizations
            .CountAsync(o => !o.IsDeleted, cancellationToken);

        var totalIndividuals = await _dbContext.IndividualProfiles
            .CountAsync(cancellationToken);

        var pendingKycUsers = await _dbContext.IndividualProfiles
            .CountAsync(p => p.KycStatus == KycStatus.Pending, cancellationToken);

        var rejectedKycUsers = await _dbContext.IndividualProfiles
            .CountAsync(p => p.KycStatus == KycStatus.Rejected, cancellationToken);

        var activeUsers = await _dbContext.RefreshTokens
            .Where(t => t.RevokedAtUtc == null && t.ExpiresAtUtc > now)
            .Select(t => t.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var activeSavingPlans = await _dbContext.SavingsAccounts
            .CountAsync(a => a.Status == SavingsAccountStatus.Active, cancellationToken);

        return new PlatformAdminMetricsDto(
            TotalOrganizations: totalOrganizations,
            TotalIndividuals: totalIndividuals,
            PendingKycUsers: pendingKycUsers,
            ActiveUsers: activeUsers,
            RejectedKycUsers: rejectedKycUsers,
            ActiveSavingPlans: activeSavingPlans,
            Timestamp: now);
    }
}
