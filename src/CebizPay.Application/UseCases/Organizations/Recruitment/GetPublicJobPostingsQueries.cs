using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Recruitment;

/// <summary>
/// Public query to browse active and published job openings across organizations.
/// </summary>
public sealed record GetPublicJobPostingsQuery(
    string? SearchTerm = null,
    string? Location = null,
    EmploymentType? EmploymentType = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<PublicJobPostingDto>>;

/// <summary>
/// Validator for GetPublicJobPostingsQuery.
/// </summary>
public sealed class GetPublicJobPostingsQueryValidator : AbstractValidator<GetPublicJobPostingsQuery>
{
    /// <summary>
    /// Initializes validation rules for GetPublicJobPostingsQuery.
    /// </summary>
    public GetPublicJobPostingsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetPublicJobPostingsQuery.
/// </summary>
public sealed class GetPublicJobPostingsQueryHandler : IRequestHandler<GetPublicJobPostingsQuery, PagedResult<PublicJobPostingDto>>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetPublicJobPostingsQueryHandler"/>.
    /// </summary>
    public GetPublicJobPostingsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<PublicJobPostingDto>> Handle(GetPublicJobPostingsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var query = _dbContext.JobPostings
            .Where(j => j.Status == JobPostingStatus.Published && (!j.ApplicationDeadline.HasValue || j.ApplicationDeadline.Value >= now));

        if (request.EmploymentType.HasValue)
        {
            query = query.Where(j => j.EmploymentType == request.EmploymentType.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            var loc = request.Location.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(j => j.Location != null && j.Location.ToLower().Contains(loc));
#pragma warning restore CA1862, CA1304, CA1311
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(j => j.Title.ToLower().Contains(search) || j.Description.ToLower().Contains(search));
#pragma warning restore CA1862, CA1304, CA1311
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var jobPostings = await query
            .OrderByDescending(j => j.PublishedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var orgIds = jobPostings.Select(j => j.OrganizationId).Distinct().ToList();
        var orgsList = await _dbContext.Organizations
            .Where(o => orgIds.Contains(o.Id))
            .ToListAsync(cancellationToken);
        var orgNames = orgsList.ToDictionary(o => o.Id, o => o.CompanyName);

        var deptIds = jobPostings.Where(j => j.DepartmentId.HasValue).Select(j => j.DepartmentId!.Value).Distinct().ToList();
        var deptsList = await _dbContext.Departments
            .Where(d => deptIds.Contains(d.Id))
            .ToListAsync(cancellationToken);
        var deptNames = deptsList.ToDictionary(d => d.Id, d => d.Name);

        var roleIds = jobPostings.Where(j => j.WorkforceRoleId.HasValue).Select(j => j.WorkforceRoleId!.Value).Distinct().ToList();
        var rolesList = await _dbContext.WorkforceRoles
            .Where(r => roleIds.Contains(r.Id))
            .ToListAsync(cancellationToken);
        var roleTitles = rolesList.ToDictionary(r => r.Id, r => r.Title);

        var dtos = jobPostings.Select(j =>
        {
            orgNames.TryGetValue(j.OrganizationId, out var orgName);
            deptNames.TryGetValue(j.DepartmentId ?? Guid.Empty, out var deptName);
            roleTitles.TryGetValue(j.WorkforceRoleId ?? Guid.Empty, out var roleTitle);

            return new PublicJobPostingDto(
                j.Id,
                j.OrganizationId,
                orgName ?? "Unknown Organization",
                j.Title,
                j.Description,
                deptName,
                roleTitle,
                j.EmploymentType,
                j.Location,
                j.Requirements,
                j.Responsibilities,
                j.ApplicationDeadline,
                j.PublishedAtUtc);
        }).ToList();

        return new PagedResult<PublicJobPostingDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

/// <summary>
/// Public query to get a single active published job opening.
/// </summary>
public sealed record GetPublicJobPostingByIdQuery(Guid JobPostingId) : IRequest<PublicJobPostingDto?>;

/// <summary>
/// Handler for GetPublicJobPostingByIdQuery.
/// </summary>
public sealed class GetPublicJobPostingByIdQueryHandler : IRequestHandler<GetPublicJobPostingByIdQuery, PublicJobPostingDto?>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetPublicJobPostingByIdQueryHandler"/>.
    /// </summary>
    public GetPublicJobPostingByIdQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<PublicJobPostingDto?> Handle(GetPublicJobPostingByIdQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var job = await _dbContext.JobPostings.FirstOrDefaultAsync(
            j => j.Id == request.JobPostingId && j.Status == JobPostingStatus.Published && (!j.ApplicationDeadline.HasValue || j.ApplicationDeadline.Value >= now),
            cancellationToken);

        if (job == null)
        {
            return null;
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == job.OrganizationId, cancellationToken);
        var orgName = org?.CompanyName ?? "Unknown Organization";

        string? deptName = null;
        if (job.DepartmentId.HasValue)
        {
            var dept = await _dbContext.Departments.FirstOrDefaultAsync(d => d.Id == job.DepartmentId.Value, cancellationToken);
            deptName = dept?.Name;
        }

        string? roleTitle = null;
        if (job.WorkforceRoleId.HasValue)
        {
            var role = await _dbContext.WorkforceRoles.FirstOrDefaultAsync(r => r.Id == job.WorkforceRoleId.Value, cancellationToken);
            roleTitle = role?.Title;
        }

        return new PublicJobPostingDto(
            job.Id,
            job.OrganizationId,
            orgName,
            job.Title,
            job.Description,
            deptName,
            roleTitle,
            job.EmploymentType,
            job.Location,
            job.Requirements,
            job.Responsibilities,
            job.ApplicationDeadline,
            job.PublishedAtUtc);
    }
}
