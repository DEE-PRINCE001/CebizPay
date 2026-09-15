using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Staff;

/// <summary>
/// Data transfer object representing a staff member's savings account subscription.
/// </summary>
public sealed record StaffSavingsAccountItemDto(
    Guid Id,
    Guid SavingsPlanId,
    string PlanName,
    string PlanType,
    decimal PrincipalBalance,
    decimal AccruedInterest,
    decimal? TargetAmount,
    decimal? ContributionAmount,
    string Currency,
    string Status,
    DateTime StartDateUtc,
    DateTime MaturityDateUtc);

/// <summary>
/// Query to retrieve savings accounts and plans for a staff member.
/// </summary>
public sealed record GetStaffSavingsQuery(
    Guid OrganizationId,
    Guid MembershipId,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<StaffSavingsAccountItemDto>>;

/// <summary>
/// Validator for GetStaffSavingsQuery.
/// </summary>
public sealed class GetStaffSavingsQueryValidator : AbstractValidator<GetStaffSavingsQuery>
{
    /// <summary>
    /// Initializes validation rules.
    /// </summary>
    public GetStaffSavingsQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.MembershipId).NotEmpty().WithMessage("MembershipId is required.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for <see cref="GetStaffSavingsQuery"/>.
/// </summary>
public sealed class GetStaffSavingsQueryHandler : IRequestHandler<GetStaffSavingsQuery, PagedResult<StaffSavingsAccountItemDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetStaffSavingsQueryHandler"/>.
    /// </summary>
    public GetStaffSavingsQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<StaffSavingsAccountItemDto>> Handle(GetStaffSavingsQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var membership = await _dbContext.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.Id == request.MembershipId && m.OrganizationId == request.OrganizationId, cancellationToken)
            .ConfigureAwait(false);

        if (membership == null)
        {
            throw new KeyNotFoundException($"Staff membership '{request.MembershipId}' was not found in this organization.");
        }

        var query = _dbContext.SavingsAccounts
            .Where(s => s.OrganizationId == request.OrganizationId && s.OwnerUserId == membership.UserId);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var accounts = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (accounts.Count == 0)
        {
            return new PagedResult<StaffSavingsAccountItemDto>(Array.Empty<StaffSavingsAccountItemDto>(), totalCount, request.PageNumber, request.PageSize);
        }

        var planIds = accounts.Select(a => a.SavingsPlanId).Distinct().ToList();
        var plansList = await _dbContext.SavingsPlans
            .Where(p => planIds.Contains(p.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var planMap = plansList.ToDictionary(p => p.Id, p => p.Name);

        var dtos = accounts.Select(a =>
        {
            planMap.TryGetValue(a.SavingsPlanId, out var planName);
            return new StaffSavingsAccountItemDto(
                Id: a.Id,
                SavingsPlanId: a.SavingsPlanId,
                PlanName: planName ?? "Savings Plan",
                PlanType: a.PlanType.ToString(),
                PrincipalBalance: a.PrincipalBalance,
                AccruedInterest: a.AccruedInterest,
                TargetAmount: a.TargetAmount,
                ContributionAmount: a.ContributionAmount,
                Currency: a.Currency.ToString(),
                Status: a.Status.ToString(),
                StartDateUtc: a.StartDateUtc,
                MaturityDateUtc: a.MaturityDateUtc);
        }).ToList();

        return new PagedResult<StaffSavingsAccountItemDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
