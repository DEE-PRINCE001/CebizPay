using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Workforce;

/// <summary>
/// Query to list organization departments with search and pagination.
/// </summary>
public sealed record GetDepartmentsQuery(
    Guid OrganizationId,
    string? SearchTerm = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<DepartmentDto>>;

/// <summary>
/// Validator for GetDepartmentsQuery.
/// </summary>
public sealed class GetDepartmentsQueryValidator : AbstractValidator<GetDepartmentsQuery>
{
    /// <summary>
    /// Initializes validation rules for GetDepartmentsQuery.
    /// </summary>
    public GetDepartmentsQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetDepartmentsQuery.
/// </summary>
public sealed class GetDepartmentsQueryHandler : IRequestHandler<GetDepartmentsQuery, PagedResult<DepartmentDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetDepartmentsQueryHandler"/>.
    /// </summary>
    public GetDepartmentsQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<DepartmentDto>> Handle(GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var query = _dbContext.Departments.Where(d => d.OrganizationId == request.OrganizationId);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(d => d.Name.ToLower().Contains(search) || (d.Description != null && d.Description.ToLower().Contains(search)));
#pragma warning restore CA1862, CA1304, CA1311
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var departments = await query
            .OrderBy(d => d.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var deptIds = departments.Select(d => d.Id).ToList();

        var staffCountsList = await _dbContext.OrganizationMemberships
            .Where(m => m.OrganizationId == request.OrganizationId && m.DepartmentId != null && deptIds.Contains(m.DepartmentId.Value) && m.Status == MembershipStatus.Active)
            .GroupBy(m => m.DepartmentId!.Value)
            .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var staffCounts = staffCountsList.ToDictionary(x => x.DepartmentId, x => x.Count);

        var dtos = departments.Select(d => new DepartmentDto(
            d.Id,
            d.OrganizationId,
            d.Name,
            d.Description,
            d.CreatedAtUtc,
            staffCounts.TryGetValue(d.Id, out var count) ? count : 0
        )).ToList();

        return new PagedResult<DepartmentDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

/// <summary>
/// Query to get a single department by ID.
/// </summary>
public sealed record GetDepartmentByIdQuery(
    Guid DepartmentId,
    Guid OrganizationId) : IRequest<DepartmentDto?>;

/// <summary>
/// Handler for GetDepartmentByIdQuery.
/// </summary>
public sealed class GetDepartmentByIdQueryHandler : IRequestHandler<GetDepartmentByIdQuery, DepartmentDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetDepartmentByIdQueryHandler"/>.
    /// </summary>
    public GetDepartmentByIdQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<DepartmentDto?> Handle(GetDepartmentByIdQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var dept = await _dbContext.Departments.FirstOrDefaultAsync(
            d => d.Id == request.DepartmentId && d.OrganizationId == request.OrganizationId,
            cancellationToken);

        if (dept == null)
        {
            return null;
        }

        var staffCount = await _dbContext.OrganizationMemberships.CountAsync(
            m => m.OrganizationId == request.OrganizationId && m.DepartmentId == dept.Id && m.Status == MembershipStatus.Active,
            cancellationToken);

        return new DepartmentDto(
            dept.Id,
            dept.OrganizationId,
            dept.Name,
            dept.Description,
            dept.CreatedAtUtc,
            staffCount);
    }
}
