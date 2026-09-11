using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Organizations;

/// <summary>
/// Query to retrieve submitted KYB verification documents for a specific organization.
/// </summary>
public sealed record GetAdminOrganizationDocumentsQuery(Guid OrganizationId) : IRequest<AdminOrganizationDocumentsResponseDto>;

/// <summary>
/// Validator for GetAdminOrganizationDocumentsQuery.
/// </summary>
public sealed class GetAdminOrganizationDocumentsQueryValidator : AbstractValidator<GetAdminOrganizationDocumentsQuery>
{
    /// <summary>
    /// Initializes validation rules for GetAdminOrganizationDocumentsQuery.
    /// </summary>
    public GetAdminOrganizationDocumentsQueryValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("Organization ID must not be empty.");
    }
}

/// <summary>
/// Handler for GetAdminOrganizationDocumentsQuery.
/// </summary>
public sealed class GetAdminOrganizationDocumentsQueryHandler : IRequestHandler<GetAdminOrganizationDocumentsQuery, AdminOrganizationDocumentsResponseDto>
{
    private readonly IApplicationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminOrganizationDocumentsQueryHandler"/>.
    /// </summary>
    public GetAdminOrganizationDocumentsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public async Task<AdminOrganizationDocumentsResponseDto> Handle(GetAdminOrganizationDocumentsQuery request, CancellationToken cancellationToken)
    {
        var org = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId && !o.IsDeleted, cancellationToken);

        if (org == null)
        {
            throw new KeyNotFoundException($"Organization with ID '{request.OrganizationId}' was not found.");
        }

        var kybDetails = await _dbContext.KybDetails
            .Where(k => k.OrganizationId == request.OrganizationId)
            .OrderByDescending(k => k.SubmittedAtUtc)
            .ToListAsync(cancellationToken);

        var documents = new List<AdminOrganizationDocumentDto>();

        var step2Detail = kybDetails.FirstOrDefault(k => k.Step == 2 && !string.IsNullOrWhiteSpace(k.CacCertificateUrl));
        var cacUrl = step2Detail?.CacCertificateUrl ?? org.CacCertificateUrl;

        if (!string.IsNullOrWhiteSpace(cacUrl))
        {
            documents.Add(new AdminOrganizationDocumentDto(
                step2Detail?.Id.ToString() ?? $"doc-{org.Id:N}"[..8],
                "Corporative Association Community",
                "CAC_CERTIFICATE",
                cacUrl,
                null,
                step2Detail?.SubmittedAtUtc ?? org.CreatedAtUtc));
        }

        return new AdminOrganizationDocumentsResponseDto(org.Id, documents);
    }
}
