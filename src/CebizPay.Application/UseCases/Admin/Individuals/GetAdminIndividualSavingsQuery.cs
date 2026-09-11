using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Individuals;

/// <summary>
/// Query to retrieve savings plans and subscription contracts for an individual user.
/// </summary>
public sealed record GetAdminIndividualSavingsQuery(string Id) : IRequest<AdminIndividualSavingsListDto>;

/// <summary>
/// Handler for <see cref="GetAdminIndividualSavingsQuery"/>.
/// </summary>
public sealed class GetAdminIndividualSavingsQueryHandler : IRequestHandler<GetAdminIndividualSavingsQuery, AdminIndividualSavingsListDto>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminIndividualSavingsQueryHandler"/>.
    /// </summary>
    public GetAdminIndividualSavingsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<AdminIndividualSavingsListDto> Handle(
        GetAdminIndividualSavingsQuery request,
        CancellationToken cancellationToken)
    {
        var trimmedId = request.Id.Trim();
        var isGuid = Guid.TryParse(trimmedId, out var parsedGuid);

        var profile = await _dbContext.IndividualProfiles
            .FirstOrDefaultAsync(p => (isGuid && p.Id == parsedGuid) || p.UserId == trimmedId, cancellationToken);

        if (profile == null)
        {
            throw new KeyNotFoundException($"Individual '{request.Id}' was not found.");
        }

        var accounts = await _dbContext.SavingsAccounts
            .Where(a => a.OwnerUserId == profile.UserId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (accounts.Count == 0)
        {
            return new AdminIndividualSavingsListDto(Array.Empty<AdminIndividualSavingsItemDto>(), 0);
        }

        var planIds = accounts.Select(a => a.SavingsPlanId).Distinct().ToList();
        var plans = await _dbContext.SavingsPlans
            .Where(p => planIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        var planMap = plans.ToDictionary(p => p.Id, p => p.Name);

        var items = accounts.Select(a => new AdminIndividualSavingsItemDto(
            a.Id.ToString(),
            planMap.GetValueOrDefault(a.SavingsPlanId, "Target Savings"),
            a.TargetAmount,
            a.PrincipalBalance,
            a.ContributionFrequency?.ToString() ?? "Monthly",
            a.InterestRateSnapshot <= 1.0m ? a.InterestRateSnapshot * 100m : a.InterestRateSnapshot,
            a.StartDateUtc,
            a.MaturityDateUtc,
            a.Status.ToString())).ToList();

        return new AdminIndividualSavingsListDto(items, items.Count);
    }
}
