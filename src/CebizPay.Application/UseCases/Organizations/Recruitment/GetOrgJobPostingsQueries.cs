using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Recruitment;

/// <summary>
/// Query to list organization job postings with optional filters, search, and pagination.
/// </summary>
public sealed record GetOrgJobPostingsQuery(
    Guid OrganizationId,
    JobPostingStatus? Status = null,
    Guid? DepartmentId = null,
    Guid? WorkforceRoleId = null,
    string? SearchTerm = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<JobPostingDto>>;

/// <summary>
/// Validator for GetOrgJobPostingsQuery.
/// </summary>
public sealed class GetOrgJobPostingsQueryValidator : AbstractValidator<GetOrgJobPostingsQuery>
{
    /// <summary>
    /// Initializes validation rules for GetOrgJobPostingsQuery.
    /// </summary>
    public GetOrgJobPostingsQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetOrgJobPostingsQuery.
/// </summary>
public sealed class GetOrgJobPostingsQueryHandler : IRequestHandler<GetOrgJobPostingsQuery, PagedResult<JobPostingDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetOrgJobPostingsQueryHandler"/>.
    /// </summary>
    public GetOrgJobPostingsQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<JobPostingDto>> Handle(GetOrgJobPostingsQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var query = _dbContext.JobPostings.Where(j => j.OrganizationId == request.OrganizationId);

        if (request.Status.HasValue)
        {
            query = query.Where(j => j.Status == request.Status.Value);
        }

        if (request.DepartmentId.HasValue)
        {
            query = query.Where(j => j.DepartmentId == request.DepartmentId.Value);
        }

        if (request.WorkforceRoleId.HasValue)
        {
            query = query.Where(j => j.WorkforceRoleId == request.WorkforceRoleId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(j => j.Title.ToLower().Contains(search) || j.Description.ToLower().Contains(search) || (j.Location != null && j.Location.ToLower().Contains(search)));
#pragma warning restore CA1862, CA1304, CA1311
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var jobPostings = await query
            .OrderByDescending(j => j.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var jobIds = jobPostings.Select(j => j.Id).ToList();

        // Application counts per job posting
        var appCountsList = await _dbContext.RecruitmentApplications
            .Where(a => a.OrganizationId == request.OrganizationId && jobIds.Contains(a.JobPostingId))
            .GroupBy(a => a.JobPostingId)
            .Select(g => new { JobPostingId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var appCounts = appCountsList.ToDictionary(x => x.JobPostingId, x => x.Count);

        // Metadata lookups
        var deptIds = jobPostings.Where(j => j.DepartmentId.HasValue).Select(j => j.DepartmentId!.Value).Distinct().ToList();
        var departmentsList = await _dbContext.Departments
            .Where(d => d.OrganizationId == request.OrganizationId && deptIds.Contains(d.Id))
            .ToListAsync(cancellationToken);
        var departments = departmentsList.ToDictionary(d => d.Id, d => d.Name);

        var roleIds = jobPostings.Where(j => j.WorkforceRoleId.HasValue).Select(j => j.WorkforceRoleId!.Value).Distinct().ToList();
        var rolesList = await _dbContext.WorkforceRoles
            .Where(r => r.OrganizationId == request.OrganizationId && roleIds.Contains(r.Id))
            .ToListAsync(cancellationToken);
        var roles = rolesList.ToDictionary(r => r.Id, r => r.Title);

        var levelIds = jobPostings.Where(j => j.SalaryLevelId.HasValue).Select(j => j.SalaryLevelId!.Value).Distinct().ToList();
        var salaryLevelsList = await _dbContext.SalaryLevels
            .Where(s => s.OrganizationId == request.OrganizationId && levelIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
        var salaryLevels = salaryLevelsList.ToDictionary(s => s.Id, s => (s.LevelName, s.BaseAmount, s.Currency));

        var dtos = jobPostings.Select(j =>
        {
            departments.TryGetValue(j.DepartmentId ?? Guid.Empty, out var deptName);
            roles.TryGetValue(j.WorkforceRoleId ?? Guid.Empty, out var roleTitle);
            var (lvlName, baseAmt, curr) = j.SalaryLevelId.HasValue && salaryLevels.TryGetValue(j.SalaryLevelId.Value, out var lvl)
                ? (lvl.LevelName, (decimal?)lvl.BaseAmount, lvl.Currency)
                : (null, null, null);

            return new JobPostingDto(
                j.Id,
                j.OrganizationId,
                j.Title,
                j.Description,
                j.DepartmentId,
                deptName,
                j.WorkforceRoleId,
                roleTitle,
                j.SalaryLevelId,
                lvlName,
                baseAmt,
                curr,
                j.EmploymentType,
                j.Location,
                j.Requirements,
                j.Responsibilities,
                j.ApplicationDeadline,
                j.Status,
                j.PublishedAtUtc,
                j.ClosedAtUtc,
                j.CreatedByUserId,
                j.CreatedAtUtc,
                j.UpdatedAtUtc,
                appCounts.TryGetValue(j.Id, out var count) ? count : 0);
        }).ToList();

        return new PagedResult<JobPostingDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

/// <summary>
/// Query to get single organization job posting details.
/// </summary>
public sealed record GetOrgJobPostingByIdQuery(
    Guid JobPostingId,
    Guid OrganizationId) : IRequest<JobPostingDto?>;

/// <summary>
/// Handler for GetOrgJobPostingByIdQuery.
/// </summary>
public sealed class GetOrgJobPostingByIdQueryHandler : IRequestHandler<GetOrgJobPostingByIdQuery, JobPostingDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetOrgJobPostingByIdQueryHandler"/>.
    /// </summary>
    public GetOrgJobPostingByIdQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<JobPostingDto?> Handle(GetOrgJobPostingByIdQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var job = await _dbContext.JobPostings.FirstOrDefaultAsync(
            j => j.Id == request.JobPostingId && j.OrganizationId == request.OrganizationId,
            cancellationToken);

        if (job == null)
        {
            return null;
        }

        string? deptName = null;
        if (job.DepartmentId.HasValue)
        {
            var dept = await _dbContext.Departments.FirstOrDefaultAsync(
                d => d.Id == job.DepartmentId.Value && d.OrganizationId == request.OrganizationId,
                cancellationToken);
            deptName = dept?.Name;
        }

        string? roleTitle = null;
        if (job.WorkforceRoleId.HasValue)
        {
            var role = await _dbContext.WorkforceRoles.FirstOrDefaultAsync(
                r => r.Id == job.WorkforceRoleId.Value && r.OrganizationId == request.OrganizationId,
                cancellationToken);
            roleTitle = role?.Title;
        }

        string? levelName = null;
        decimal? baseAmount = null;
        string? currency = null;
        if (job.SalaryLevelId.HasValue)
        {
            var level = await _dbContext.SalaryLevels.FirstOrDefaultAsync(
                s => s.Id == job.SalaryLevelId.Value && s.OrganizationId == request.OrganizationId,
                cancellationToken);
            if (level != null)
            {
                levelName = level.LevelName;
                baseAmount = level.BaseAmount;
                currency = level.Currency;
            }
        }

        var appCount = await _dbContext.RecruitmentApplications.CountAsync(
            a => a.JobPostingId == job.Id && a.OrganizationId == request.OrganizationId,
            cancellationToken);

        return new JobPostingDto(
            job.Id,
            job.OrganizationId,
            job.Title,
            job.Description,
            job.DepartmentId,
            deptName,
            job.WorkforceRoleId,
            roleTitle,
            job.SalaryLevelId,
            levelName,
            baseAmount,
            currency,
            job.EmploymentType,
            job.Location,
            job.Requirements,
            job.Responsibilities,
            job.ApplicationDeadline,
            job.Status,
            job.PublishedAtUtc,
            job.ClosedAtUtc,
            job.CreatedByUserId,
            job.CreatedAtUtc,
            job.UpdatedAtUtc,
            appCount);
    }
}
