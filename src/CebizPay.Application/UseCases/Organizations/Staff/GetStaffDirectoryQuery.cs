using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Staff;

/// <summary>
/// Query to retrieve a paginated directory of organization staff members with filtering and search.
/// </summary>
public sealed record GetStaffDirectoryQuery(
    Guid OrganizationId,
    string? SearchTerm = null,
    Guid? DepartmentId = null,
    Guid? WorkforceRoleId = null,
    Guid? SalaryLevelId = null,
    MembershipStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<StaffSummaryDto>>;

/// <summary>
/// Validator for GetStaffDirectoryQuery.
/// </summary>
public sealed class GetStaffDirectoryQueryValidator : AbstractValidator<GetStaffDirectoryQuery>
{
    /// <summary>
    /// Initializes validation rules for GetStaffDirectoryQuery.
    /// </summary>
    public GetStaffDirectoryQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetStaffDirectoryQuery.
/// </summary>
public sealed class GetStaffDirectoryQueryHandler : IRequestHandler<GetStaffDirectoryQuery, PagedResult<StaffSummaryDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly IIdentityService _identityService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetStaffDirectoryQueryHandler"/>.
    /// </summary>
    public GetStaffDirectoryQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        IIdentityService identityService)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _identityService = identityService;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<StaffSummaryDto>> Handle(GetStaffDirectoryQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var query = _dbContext.OrganizationMemberships.Where(m => m.OrganizationId == request.OrganizationId);

        if (request.DepartmentId.HasValue)
        {
            query = query.Where(m => m.DepartmentId == request.DepartmentId.Value);
        }

        if (request.WorkforceRoleId.HasValue)
        {
            query = query.Where(m => m.WorkforceRoleId == request.WorkforceRoleId.Value);
        }

        if (request.SalaryLevelId.HasValue)
        {
            query = query.Where(m => m.SalaryLevelId == request.SalaryLevelId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(m => m.Status == request.Status.Value);
        }

        var memberships = await query
            .OrderByDescending(m => m.JoinedAtUtc)
            .ToListAsync(cancellationToken);

        var userIds = memberships.Select(m => m.UserId).Distinct().ToList();

        var profilesList = await _dbContext.IndividualProfiles
            .Where(p => userIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);
        var profiles = profilesList.ToDictionary(p => p.UserId, p => p);

        var userDetails = await _identityService.GetUserDetailsByIdsAsync(userIds, cancellationToken);

        var deptIds = memberships.Where(m => m.DepartmentId.HasValue).Select(m => m.DepartmentId!.Value).Distinct().ToList();
        var deptsList = await _dbContext.Departments
            .Where(d => d.OrganizationId == request.OrganizationId && deptIds.Contains(d.Id))
            .ToListAsync(cancellationToken);
        var departments = deptsList.ToDictionary(d => d.Id, d => d.Name);

        var roleIds = memberships.Where(m => m.WorkforceRoleId.HasValue).Select(m => m.WorkforceRoleId!.Value).Distinct().ToList();
        var rolesList = await _dbContext.WorkforceRoles
            .Where(r => r.OrganizationId == request.OrganizationId && roleIds.Contains(r.Id))
            .ToListAsync(cancellationToken);
        var roles = rolesList.ToDictionary(r => r.Id, r => r.Title);

        var levelIds = memberships.Where(m => m.SalaryLevelId.HasValue).Select(m => m.SalaryLevelId!.Value).Distinct().ToList();
        var levelsList = await _dbContext.SalaryLevels
            .Where(s => s.OrganizationId == request.OrganizationId && levelIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
        var levels = levelsList.ToDictionary(s => s.Id, s => s);

        var items = memberships.Select(m =>
        {
            profiles.TryGetValue(m.UserId, out var prof);
            userDetails.TryGetValue(m.UserId, out var userDet);
            departments.TryGetValue(m.DepartmentId ?? Guid.Empty, out var deptName);
            roles.TryGetValue(m.WorkforceRoleId ?? Guid.Empty, out var roleTitle);
            levels.TryGetValue(m.SalaryLevelId ?? Guid.Empty, out var salLevel);

            return new StaffSummaryDto(
                m.Id,
                m.UserId,
                prof?.FirstName,
                prof?.LastName,
                userDet.Email,
                userDet.PhoneNumber,
                prof?.KycStatus.ToString(),
                m.DepartmentId,
                deptName,
                m.WorkforceRoleId,
                roleTitle,
                m.SalaryLevelId,
                salLevel?.LevelName,
                salLevel?.BaseAmount,
                salLevel?.Currency,
                m.Role.ToString(),
                m.Status.ToString(),
                m.JoinedAtUtc,
                m.SuspendedAtUtc,
                m.SuspensionReason);
        });

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Trim();
            items = items.Where(s =>
                (!string.IsNullOrEmpty(s.FirstName) && s.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(s.LastName) && s.LastName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(s.Email) && s.Email.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(s.PhoneNumber) && s.PhoneNumber.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(s.DepartmentName) && s.DepartmentName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(s.RoleTitle) && s.RoleTitle.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        var filteredList = items.ToList();
        var totalCount = filteredList.Count;

        var pagedItems = filteredList
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PagedResult<StaffSummaryDto>(pagedItems, totalCount, request.PageNumber, request.PageSize);
    }
}
