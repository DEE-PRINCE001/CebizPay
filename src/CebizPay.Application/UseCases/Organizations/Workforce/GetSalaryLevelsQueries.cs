using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Workforce;

/// <summary>
/// Query to list salary levels with optional currency filter, search, and pagination.
/// </summary>
public sealed record GetSalaryLevelsQuery(
    Guid OrganizationId,
    string? Currency = null,
    string? SearchTerm = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<SalaryLevelDto>>;

/// <summary>
/// Validator for GetSalaryLevelsQuery.
/// </summary>
public sealed class GetSalaryLevelsQueryValidator : AbstractValidator<GetSalaryLevelsQuery>
{
    /// <summary>
    /// Initializes validation rules for GetSalaryLevelsQuery.
    /// </summary>
    public GetSalaryLevelsQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetSalaryLevelsQuery.
/// </summary>
public sealed class GetSalaryLevelsQueryHandler : IRequestHandler<GetSalaryLevelsQuery, PagedResult<SalaryLevelDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetSalaryLevelsQueryHandler"/>.
    /// </summary>
    public GetSalaryLevelsQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<SalaryLevelDto>> Handle(GetSalaryLevelsQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var query = _dbContext.SalaryLevels.Where(s => s.OrganizationId == request.OrganizationId);

        if (!string.IsNullOrWhiteSpace(request.Currency))
        {
            var curr = request.Currency.Trim().ToUpperInvariant();
            query = query.Where(s => s.Currency == curr);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(s => s.LevelName.ToLower().Contains(search));
#pragma warning restore CA1862, CA1304, CA1311
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var salaryLevels = await query
            .OrderBy(s => s.BaseAmount)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var levelIds = salaryLevels.Select(s => s.Id).ToList();

        var staffCountsList = await _dbContext.OrganizationMemberships
            .Where(m => m.OrganizationId == request.OrganizationId && m.SalaryLevelId != null && levelIds.Contains(m.SalaryLevelId.Value) && m.Status == MembershipStatus.Active)
            .GroupBy(m => m.SalaryLevelId!.Value)
            .Select(g => new { SalaryLevelId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var staffCounts = staffCountsList.ToDictionary(x => x.SalaryLevelId, x => x.Count);

        var dtos = salaryLevels.Select(s => new SalaryLevelDto(
            s.Id,
            s.OrganizationId,
            s.LevelName,
            s.BaseAmount,
            s.Currency,
            s.CreatedAtUtc,
            staffCounts.TryGetValue(s.Id, out var count) ? count : 0
        )).ToList();

        return new PagedResult<SalaryLevelDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

/// <summary>
/// Query to get a single salary level by ID.
/// </summary>
public sealed record GetSalaryLevelByIdQuery(
    Guid SalaryLevelId,
    Guid OrganizationId) : IRequest<SalaryLevelDto?>;

/// <summary>
/// Handler for GetSalaryLevelByIdQuery.
/// </summary>
public sealed class GetSalaryLevelByIdQueryHandler : IRequestHandler<GetSalaryLevelByIdQuery, SalaryLevelDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetSalaryLevelByIdQueryHandler"/>.
    /// </summary>
    public GetSalaryLevelByIdQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<SalaryLevelDto?> Handle(GetSalaryLevelByIdQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var salaryLevel = await _dbContext.SalaryLevels.FirstOrDefaultAsync(
            s => s.Id == request.SalaryLevelId && s.OrganizationId == request.OrganizationId,
            cancellationToken);

        if (salaryLevel == null)
        {
            return null;
        }

        var staffCount = await _dbContext.OrganizationMemberships.CountAsync(
            m => m.OrganizationId == request.OrganizationId && m.SalaryLevelId == salaryLevel.Id && m.Status == MembershipStatus.Active,
            cancellationToken);

        return new SalaryLevelDto(
            salaryLevel.Id,
            salaryLevel.OrganizationId,
            salaryLevel.LevelName,
            salaryLevel.BaseAmount,
            salaryLevel.Currency,
            salaryLevel.CreatedAtUtc,
            staffCount);
    }
}
