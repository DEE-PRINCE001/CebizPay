using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Organizations;

/// <summary>
/// Query to retrieve a paginated directory of platform organizations with filtering and search.
/// </summary>
public sealed record GetAdminOrganizationsDirectoryQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Search = null,
    string? Status = null,
    string? Category = null) : IRequest<PagedResult<AdminOrganizationSummaryDto>>;

/// <summary>
/// Validator for GetAdminOrganizationsDirectoryQuery.
/// </summary>
public sealed class GetAdminOrganizationsDirectoryQueryValidator : AbstractValidator<GetAdminOrganizationsDirectoryQuery>
{
    /// <summary>
    /// Initializes validation rules for GetAdminOrganizationsDirectoryQuery.
    /// </summary>
    public GetAdminOrganizationsDirectoryQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetAdminOrganizationsDirectoryQuery.
/// </summary>
public sealed class GetAdminOrganizationsDirectoryQueryHandler : IRequestHandler<GetAdminOrganizationsDirectoryQuery, PagedResult<AdminOrganizationSummaryDto>>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminOrganizationsDirectoryQueryHandler"/>.
    /// </summary>
    public GetAdminOrganizationsDirectoryQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<AdminOrganizationSummaryDto>> Handle(GetAdminOrganizationsDirectoryQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.Organizations.Where(o => !o.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(o =>
                o.CompanyName.ToLower().Contains(search) ||
                o.Email.ToLower().Contains(search) ||
                (o.Address != null && o.Address.ToLower().Contains(search)));
#pragma warning restore CA1862, CA1304, CA1311
        }

        var parsedStatus = ParseStatus(request.Status);
        if (parsedStatus.HasValue)
        {
            query = query.Where(o => o.Status == parsedStatus.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var category = request.Category.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(o => o.Category != null && o.Category.ToLower() == category);
#pragma warning restore CA1862, CA1304, CA1311
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var organizations = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var orgIds = organizations.Select(o => o.Id).ToList();

        var memberships = await _dbContext.OrganizationMemberships
            .Where(m => orgIds.Contains(m.OrganizationId) && m.Status != MembershipStatus.Terminated)
            .ToListAsync(cancellationToken);

        var staffCounts = memberships
            .GroupBy(m => m.OrganizationId)
            .ToDictionary(g => g.Key, g => g.Count());

        var items = organizations.Select(o => new AdminOrganizationSummaryDto(
            o.Id,
            o.CompanyName,
            o.Category,
            o.Email,
            o.Address,
            o.Status.ToString(),
            staffCounts.GetValueOrDefault(o.Id, 0),
            o.LogoUrl,
            o.CreatedAtUtc)).ToList();

        return new PagedResult<AdminOrganizationSummaryDto>(items, totalCount, request.PageNumber, request.PageSize);
    }

    internal static OrganizationStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var trimmed = status.Trim();

        if (int.TryParse(trimmed, out var intVal) && Enum.IsDefined(typeof(OrganizationStatus), intVal))
        {
            return (OrganizationStatus)intVal;
        }

        if (Enum.TryParse<OrganizationStatus>(trimmed, true, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
