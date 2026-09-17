using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Profile;

/// <summary>
/// Query to retrieve corporate profile and KYB registration details for an organization tenant.
/// </summary>
public sealed record GetOrgProfileQuery(Guid OrganizationId) : IRequest<OrgProfileDto?>;

/// <summary>
/// Validator for <see cref="GetOrgProfileQuery"/>.
/// </summary>
public sealed class GetOrgProfileQueryValidator : AbstractValidator<GetOrgProfileQuery>
{
    /// <summary>
    /// Initializes validation rules.
    /// </summary>
    public GetOrgProfileQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
    }
}

/// <summary>
/// Handler for <see cref="GetOrgProfileQuery"/>.
/// </summary>
public sealed class GetOrgProfileQueryHandler : IRequestHandler<GetOrgProfileQuery, OrgProfileDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetOrgProfileQueryHandler"/>.
    /// </summary>
    public GetOrgProfileQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    /// <inheritdoc/>
    public async Task<OrgProfileDto?> Handle(GetOrgProfileQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var org = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId && !o.IsDeleted, cancellationToken)
            .ConfigureAwait(false);

        if (org == null)
        {
            return null;
        }

        var latestStep2Kyb = await _dbContext.KybDetails
            .Where(k => k.OrganizationId == request.OrganizationId && k.Step == 2 && !string.IsNullOrWhiteSpace(k.CacCertificateUrl))
            .OrderByDescending(k => k.SubmittedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var certUrl = latestStep2Kyb?.CacCertificateUrl ?? org.CacCertificateUrl;
        var cacNum = !string.IsNullOrWhiteSpace(org.CacNumber) ? org.CacNumber : latestStep2Kyb?.CacNumber;
        var logoUrl = !string.IsNullOrWhiteSpace(org.LogoUrl) ? org.LogoUrl : latestStep2Kyb?.LogoUrl;

        return new OrgProfileDto(
            OrganizationId: org.Id,
            Name: org.CompanyName,
            Email: org.Email,
            PhoneNumber: org.Phone,
            Address: org.Address,
            Category: org.Category,
            Status: org.Status.ToString(),
            LogoUrl: logoUrl,
            CacNumber: cacNum,
            CacCertificateUrl: certUrl,
            RegisteredAtUtc: org.CreatedAtUtc,
            PhotoUrl: org.PhotoUrl);
    }
}
