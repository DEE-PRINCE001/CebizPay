using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Organizations;

/// <summary>
/// Query to retrieve detailed operational and KYB profile for a specific organization by ID.
/// </summary>
public sealed record GetAdminOrganizationDetailsQuery(Guid Id) : IRequest<AdminOrganizationDetailsDto?>;

/// <summary>
/// Validator for GetAdminOrganizationDetailsQuery.
/// </summary>
public sealed class GetAdminOrganizationDetailsQueryValidator : AbstractValidator<GetAdminOrganizationDetailsQuery>
{
    /// <summary>
    /// Initializes validation rules for GetAdminOrganizationDetailsQuery.
    /// </summary>
    public GetAdminOrganizationDetailsQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Organization ID must not be empty.");
    }
}

/// <summary>
/// Handler for GetAdminOrganizationDetailsQuery.
/// </summary>
public sealed class GetAdminOrganizationDetailsQueryHandler : IRequestHandler<GetAdminOrganizationDetailsQuery, AdminOrganizationDetailsDto?>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminOrganizationDetailsQueryHandler"/>.
    /// </summary>
    public GetAdminOrganizationDetailsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<AdminOrganizationDetailsDto?> Handle(GetAdminOrganizationDetailsQuery request, CancellationToken cancellationToken)
    {
        var org = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.Id && !o.IsDeleted, cancellationToken);

        if (org == null)
        {
            return null;
        }

        var staffCount = await _dbContext.OrganizationMemberships
            .CountAsync(m => m.OrganizationId == request.Id && m.Status != MembershipStatus.Terminated, cancellationToken);

        var kybDetails = await _dbContext.KybDetails
            .Where(k => k.OrganizationId == request.Id)
            .OrderByDescending(k => k.SubmittedAtUtc)
            .ToListAsync(cancellationToken);

        var credentials = new List<AdminOrganizationCredentialDto>();

        var latestStep2 = kybDetails.FirstOrDefault(k => k.Step == 2 && !string.IsNullOrWhiteSpace(k.CacCertificateUrl));
        var certUrl = latestStep2?.CacCertificateUrl ?? org.CacCertificateUrl;

        if (!string.IsNullOrWhiteSpace(certUrl))
        {
            var uploadedAt = latestStep2?.SubmittedAtUtc ?? org.CreatedAtUtc;
            var docId = latestStep2?.Id.ToString() ?? $"doc-{org.Id:N}"[..8];

            credentials.Add(new AdminOrganizationCredentialDto(
                docId,
                "Corporative Association Community",
                "CAC_CERTIFICATE",
                certUrl,
                uploadedAt));
        }

        return new AdminOrganizationDetailsDto(
            org.Id,
            org.CompanyName,
            org.Category,
            org.Email,
            org.Address,
            org.Status.ToString(),
            staffCount,
            org.LogoUrl,
            org.PhotoUrl,
            org.CreatedAtUtc,
            credentials);
    }
}
