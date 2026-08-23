using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Workforce;

/// <summary>
/// Query to list workforce roles for an organization with optional department filter, search, and pagination.
/// </summary>
public sealed record GetWorkforceRolesQuery(
    Guid OrganizationId,
    Guid? DepartmentId = null,
    string? SearchTerm = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<WorkforceRoleDto>>;

/// <summary>
/// Validator for GetWorkforceRolesQuery.
/// </summary>
public sealed class GetWorkforceRolesQueryValidator : AbstractValidator<GetWorkforceRolesQuery>
{
    /// <summary>
    /// Initializes validation rules for GetWorkforceRolesQuery.
    /// </summary>
    public GetWorkforceRolesQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetWorkforceRolesQuery.
/// </summary>
public sealed class GetWorkforceRolesQueryHandler : IRequestHandler<GetWorkforceRolesQuery, PagedResult<WorkforceRoleDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetWorkforceRolesQueryHandler"/>.
    /// </summary>
    public GetWorkforceRolesQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<WorkforceRoleDto>> Handle(GetWorkforceRolesQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var query = _dbContext.WorkforceRoles.Where(r => r.OrganizationId == request.OrganizationId);

        if (request.DepartmentId.HasValue)
        {
            query = query.Where(r => r.DepartmentId == request.DepartmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(r => r.Title.ToLower().Contains(search) || (r.Description != null && r.Description.ToLower().Contains(search)));
#pragma warning restore CA1862, CA1304, CA1311
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var roles = await query
            .OrderBy(r => r.Title)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var roleIds = roles.Select(r => r.Id).ToList();

        var staffCountsList = await _dbContext.OrganizationMemberships
            .Where(m => m.OrganizationId == request.OrganizationId && m.WorkforceRoleId != null && roleIds.Contains(m.WorkforceRoleId.Value) && m.Status == MembershipStatus.Active)
            .GroupBy(m => m.WorkforceRoleId!.Value)
            .Select(g => new { WorkforceRoleId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var staffCounts = staffCountsList.ToDictionary(x => x.WorkforceRoleId, x => x.Count);

        var deptIds = roles.Where(r => r.DepartmentId.HasValue).Select(r => r.DepartmentId!.Value).Distinct().ToList();
        var deptsList = await _dbContext.Departments
            .Where(d => d.OrganizationId == request.OrganizationId && deptIds.Contains(d.Id))
            .ToListAsync(cancellationToken);
        var departments = deptsList.ToDictionary(d => d.Id, d => d.Name);

        var dtos = roles.Select(r => new WorkforceRoleDto(
            r.Id,
            r.OrganizationId,
            r.DepartmentId,
            r.DepartmentId.HasValue && departments.TryGetValue(r.DepartmentId.Value, out var dName) ? dName : null,
            r.Title,
            r.Description,
            r.CreatedAtUtc,
            staffCounts.TryGetValue(r.Id, out var count) ? count : 0
        )).ToList();

        return new PagedResult<WorkforceRoleDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

/// <summary>
/// Query to get a single workforce role by ID.
/// </summary>
public sealed record GetWorkforceRoleByIdQuery(
    Guid RoleId,
    Guid OrganizationId) : IRequest<WorkforceRoleDto?>;

/// <summary>
/// Handler for GetWorkforceRoleByIdQuery.
/// </summary>
public sealed class GetWorkforceRoleByIdQueryHandler : IRequestHandler<GetWorkforceRoleByIdQuery, WorkforceRoleDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetWorkforceRoleByIdQueryHandler"/>.
    /// </summary>
    public GetWorkforceRoleByIdQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<WorkforceRoleDto?> Handle(GetWorkforceRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var role = await _dbContext.WorkforceRoles.FirstOrDefaultAsync(
            r => r.Id == request.RoleId && r.OrganizationId == request.OrganizationId,
            cancellationToken);

        if (role == null)
        {
            return null;
        }

        string? deptName = null;
        if (role.DepartmentId.HasValue)
        {
            var dept = await _dbContext.Departments.FirstOrDefaultAsync(
                d => d.Id == role.DepartmentId.Value && d.OrganizationId == request.OrganizationId,
                cancellationToken);
            deptName = dept?.Name;
        }

        var staffCount = await _dbContext.OrganizationMemberships.CountAsync(
            m => m.OrganizationId == request.OrganizationId && m.WorkforceRoleId == role.Id && m.Status == MembershipStatus.Active,
            cancellationToken);

        return new WorkforceRoleDto(
            role.Id,
            role.OrganizationId,
            role.DepartmentId,
            deptName,
            role.Title,
            role.Description,
            role.CreatedAtUtc,
            staffCount);
    }
}
