using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Recruitment;

/// <summary>
/// Query to list applications for a job posting with optional status filter, search, and pagination.
/// </summary>
public sealed record GetJobApplicationsQuery(
    Guid JobPostingId,
    Guid OrganizationId,
    ApplicationStatus? Status = null,
    string? SearchTerm = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<RecruitmentApplicationDto>>;

/// <summary>
/// Validator for GetJobApplicationsQuery.
/// </summary>
public sealed class GetJobApplicationsQueryValidator : AbstractValidator<GetJobApplicationsQuery>
{
    /// <summary>
    /// Initializes validation rules for GetJobApplicationsQuery.
    /// </summary>
    public GetJobApplicationsQueryValidator()
    {
        RuleFor(x => x.JobPostingId).NotEmpty().WithMessage("JobPostingId is required.");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1).WithMessage("PageNumber must be at least 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");
    }
}

/// <summary>
/// Handler for GetJobApplicationsQuery.
/// </summary>
public sealed class GetJobApplicationsQueryHandler : IRequestHandler<GetJobApplicationsQuery, PagedResult<RecruitmentApplicationDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetJobApplicationsQueryHandler"/>.
    /// </summary>
    public GetJobApplicationsQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<RecruitmentApplicationDto>> Handle(GetJobApplicationsQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var jobPosting = await _dbContext.JobPostings.FirstOrDefaultAsync(
            j => j.Id == request.JobPostingId && j.OrganizationId == request.OrganizationId,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Job posting {request.JobPostingId} not found in organization {request.OrganizationId}.");

        var query = _dbContext.RecruitmentApplications.Where(
            a => a.JobPostingId == request.JobPostingId && a.OrganizationId == request.OrganizationId);

        if (request.Status.HasValue)
        {
            query = query.Where(a => a.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311
            query = query.Where(a => a.ApplicantName.ToLower().Contains(search)
                                     || a.ApplicantEmail.ToLower().Contains(search)
                                     || a.ApplicantPhone.Contains(search));
#pragma warning restore CA1862, CA1304, CA1311
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var applications = await query
            .OrderByDescending(a => a.AppliedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = applications.Select(a => new RecruitmentApplicationDto(
            a.Id,
            a.JobPostingId,
            jobPosting.Title,
            a.OrganizationId,
            a.ApplicantUserId,
            a.ApplicantName,
            a.ApplicantEmail,
            a.ApplicantPhone,
            a.ResumeReference,
            a.CoverLetter,
            a.Status,
            a.AppliedAtUtc,
            a.ReviewedAtUtc,
            a.ReviewedByUserId,
            a.RejectionReason,
            a.ReviewNotes)).ToList();

        return new PagedResult<RecruitmentApplicationDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

/// <summary>
/// Query to get detailed application by ID.
/// </summary>
public sealed record GetApplicationByIdQuery(
    Guid ApplicationId,
    Guid OrganizationId) : IRequest<RecruitmentApplicationDto?>;

/// <summary>
/// Handler for GetApplicationByIdQuery.
/// </summary>
public sealed class GetApplicationByIdQueryHandler : IRequestHandler<GetApplicationByIdQuery, RecruitmentApplicationDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetApplicationByIdQueryHandler"/>.
    /// </summary>
    public GetApplicationByIdQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<RecruitmentApplicationDto?> Handle(GetApplicationByIdQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var application = await _dbContext.RecruitmentApplications.FirstOrDefaultAsync(
            a => a.Id == request.ApplicationId && a.OrganizationId == request.OrganizationId,
            cancellationToken);

        if (application == null)
        {
            return null;
        }

        var jobPosting = await _dbContext.JobPostings.FirstOrDefaultAsync(
            j => j.Id == application.JobPostingId,
            cancellationToken);

        return new RecruitmentApplicationDto(
            application.Id,
            application.JobPostingId,
            jobPosting?.Title ?? "Unknown Job",
            application.OrganizationId,
            application.ApplicantUserId,
            application.ApplicantName,
            application.ApplicantEmail,
            application.ApplicantPhone,
            application.ResumeReference,
            application.CoverLetter,
            application.Status,
            application.AppliedAtUtc,
            application.ReviewedAtUtc,
            application.ReviewedByUserId,
            application.RejectionReason,
            application.ReviewNotes);
    }
}
