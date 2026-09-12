using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Organizations;

/// <summary>
/// Data transfer object representing an organization's savings plan or account item in the admin view.
/// </summary>
public sealed record AdminOrganizationSavingsItemDto(
    string Id,
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    string Frequency,
    decimal InterestRate,
    DateTime StartDate,
    DateTime MaturityDate,
    string Status);

/// <summary>
/// Response envelope for organization savings plans list.
/// </summary>
public sealed record AdminOrganizationSavingsListDto(
    IReadOnlyList<AdminOrganizationSavingsItemDto> Items,
    int TotalCount);

/// <summary>
/// Query to retrieve active and fixed savings plans configured for an organization.
/// </summary>
public sealed record GetAdminOrganizationSavingsQuery(Guid OrganizationId) : IRequest<AdminOrganizationSavingsListDto>;

/// <summary>
/// Handler for <see cref="GetAdminOrganizationSavingsQuery"/>.
/// </summary>
public sealed class GetAdminOrganizationSavingsQueryHandler : IRequestHandler<GetAdminOrganizationSavingsQuery, AdminOrganizationSavingsListDto>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminOrganizationSavingsQueryHandler"/>.
    /// </summary>
    public GetAdminOrganizationSavingsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<AdminOrganizationSavingsListDto> Handle(
        GetAdminOrganizationSavingsQuery request,
        CancellationToken cancellationToken)
    {
        var org = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId && !o.IsDeleted, cancellationToken);

        if (org == null)
        {
            throw new KeyNotFoundException($"Organization '{request.OrganizationId}' was not found.");
        }

        // 1. Fetch organization savings accounts
        var accounts = await _dbContext.SavingsAccounts
            .Where(a => a.OrganizationId == request.OrganizationId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        // 2. Fetch organization savings plans
        var plans = await _dbContext.SavingsPlans
            .Where(p => p.OrganizationId == request.OrganizationId)
            .ToListAsync(cancellationToken);
        var planMap = plans.ToDictionary(p => p.Id);

        var resultItems = new List<AdminOrganizationSavingsItemDto>();
        var accountedPlanIds = new HashSet<Guid>();

        // Map existing savings accounts
        foreach (var account in accounts)
        {
            planMap.TryGetValue(account.SavingsPlanId, out var plan);
            accountedPlanIds.Add(account.SavingsPlanId);

            var planName = plan?.Name ?? "Corporate Reserve Fund";
            var targetAmount = account.TargetAmount ?? plan?.TargetAmount ?? 0m;
            var frequency = (account.ContributionFrequency ?? plan?.ContributionFrequency)?.ToString() ?? "Monthly";
            var rawRate = account.InterestRateSnapshot > 0 ? account.InterestRateSnapshot : (plan != null ? plan.InterestRate : 0m);
            var interestRatePercent = rawRate <= 1.0m ? rawRate * 100m : rawRate;

            resultItems.Add(new AdminOrganizationSavingsItemDto(
                $"sav-org-{account.Id.ToString("N")[..6]}",
                planName,
                targetAmount,
                account.PrincipalBalance,
                frequency,
                interestRatePercent,
                account.StartDateUtc,
                account.MaturityDateUtc,
                account.Status.ToString()));
        }

        // Include plans that do not yet have an active account instance
        foreach (var plan in plans.Where(p => !accountedPlanIds.Contains(p.Id)))
        {
            var rawRate = plan.InterestRate;
            var interestRatePercent = rawRate <= 1.0m ? rawRate * 100m : rawRate;
            var minDays = plan.MinimumDurationDays > 0 ? plan.MinimumDurationDays : 365;

            resultItems.Add(new AdminOrganizationSavingsItemDto(
                $"sav-org-{plan.Id.ToString("N")[..6]}",
                plan.Name,
                plan.TargetAmount ?? 0m,
                0m,
                plan.ContributionFrequency?.ToString() ?? "Monthly",
                interestRatePercent,
                plan.CreatedAtUtc,
                plan.CreatedAtUtc.AddDays(minDays),
                plan.IsActive ? "Active" : "Inactive"));
        }

        return new AdminOrganizationSavingsListDto(resultItems, resultItems.Count);
    }
}
