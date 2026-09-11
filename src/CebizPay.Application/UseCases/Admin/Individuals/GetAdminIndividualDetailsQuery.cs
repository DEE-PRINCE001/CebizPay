using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Individuals;

/// <summary>
/// Query to retrieve full profile and KYC details for an individual user by profile ID or user ID.
/// </summary>
public sealed record GetAdminIndividualDetailsQuery(string Id) : IRequest<AdminIndividualDetailsDto?>;

/// <summary>
/// Handler for <see cref="GetAdminIndividualDetailsQuery"/>.
/// </summary>
public sealed class GetAdminIndividualDetailsQueryHandler : IRequestHandler<GetAdminIndividualDetailsQuery, AdminIndividualDetailsDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IIdentityService _identityService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetAdminIndividualDetailsQueryHandler"/>.
    /// </summary>
    public GetAdminIndividualDetailsQueryHandler(
        IApplicationDbContext dbContext,
        IIdentityService identityService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
    }

    /// <inheritdoc/>
    public async Task<AdminIndividualDetailsDto?> Handle(
        GetAdminIndividualDetailsQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Id))
        {
            return null;
        }

        var trimmedId = request.Id.Trim();
        var isGuid = Guid.TryParse(trimmedId, out var parsedGuid);

        var profile = await _dbContext.IndividualProfiles
            .FirstOrDefaultAsync(p => (isGuid && p.Id == parsedGuid) || p.UserId == trimmedId, cancellationToken);

        if (profile == null)
        {
            return null;
        }

        // Fetch identity details
        var userDetailsMap = await _identityService.GetUserDetailsWithLockoutByIdsAsync([profile.UserId], cancellationToken);
        userDetailsMap.TryGetValue(profile.UserId, out var identityDetails);

        // Fetch employer company name
        var membership = await _dbContext.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.UserId == profile.UserId && m.Status == MembershipStatus.Active, cancellationToken);

        string companyName = "None";
        if (membership != null)
        {
            var org = await _dbContext.Organizations
                .FirstOrDefaultAsync(o => o.Id == membership.OrganizationId, cancellationToken);
            if (org != null)
            {
                companyName = org.CompanyName;
            }
        }

        // Fetch credentials (KYC documents)
        var kycDocs = await _dbContext.KycDocuments
            .Where(d => d.UserId == profile.UserId)
            .OrderByDescending(d => d.SubmittedAtUtc)
            .ToListAsync(cancellationToken);

        var credentials = kycDocs.Select(doc => new AdminIndividualCredentialDto(
            doc.Id.ToString(),
            GetDocumentTitle(doc.DocumentType),
            doc.DocumentType.ToString(),
            doc.DocumentNumber,
            doc.DocumentUrl,
            doc.SubmittedAtUtc)).ToList();

        var isSuspended = identityDetails.IsLockedOut;
        var displayStatus = isSuspended ? "Suspended" : (profile.KycStatus == KycStatus.Verified ? "Active" : profile.KycStatus.ToString());
        var professionalStatusStr = profile.ProfessionalStatus == ProfessionalStatus.Staff ? "Staff" : "Not-a-Staff";
        var fullName = $"{profile.FirstName} {profile.LastName}".Trim();

        return new AdminIndividualDetailsDto(
            profile.Id,
            fullName,
            identityDetails.Email ?? string.Empty,
            identityDetails.PhoneNumber ?? string.Empty,
            displayStatus,
            professionalStatusStr,
            companyName,
            profile.AvatarUrl,
            profile.CreatedAtUtc,
            credentials);
    }

    private static string GetDocumentTitle(DocumentType type) => type switch
    {
        DocumentType.Nimc => "National Identity Card",
        DocumentType.DriversLicense => "Driver's License",
        DocumentType.InternationalPassport => "International Passport",
        DocumentType.Liveness => "Liveness Biometric Verification",
        _ => type.ToString()
    };
}
