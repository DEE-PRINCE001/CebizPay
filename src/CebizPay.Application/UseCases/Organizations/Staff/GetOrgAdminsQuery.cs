using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Staff;

/// <summary>
/// Data transfer object representing an administrative member (Owner or Admin) of an organization.
/// </summary>
public sealed record OrgAdminSummaryDto(
    Guid MembershipId,
    string UserId,
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber,
    string Role,
    string Status,
    DateTime JoinedAtUtc);

/// <summary>
/// Query to retrieve all active administrators (Owner and Admin roles) for an organization.
/// </summary>
public sealed record GetOrgAdminsQuery(Guid OrganizationId) : IRequest<IReadOnlyList<OrgAdminSummaryDto>>;

/// <summary>
/// Validator for <see cref="GetOrgAdminsQuery"/>.
/// </summary>
public sealed class GetOrgAdminsQueryValidator : AbstractValidator<GetOrgAdminsQuery>
{
    /// <summary>
    /// Initializes validation rules.
    /// </summary>
    public GetOrgAdminsQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
    }
}

/// <summary>
/// Handler for <see cref="GetOrgAdminsQuery"/>.
/// </summary>
public sealed class GetOrgAdminsQueryHandler : IRequestHandler<GetOrgAdminsQuery, IReadOnlyList<OrgAdminSummaryDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly IIdentityService _identityService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetOrgAdminsQueryHandler"/>.
    /// </summary>
    public GetOrgAdminsQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        IIdentityService identityService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OrgAdminSummaryDto>> Handle(GetOrgAdminsQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var memberships = await _dbContext.OrganizationMemberships
            .Where(m => m.OrganizationId == request.OrganizationId &&
                        (m.Role == MembershipRoleType.Owner || m.Role == MembershipRoleType.Admin) &&
                        m.Status == MembershipStatus.Active)
            .OrderBy(m => m.Role)
            .ThenBy(m => m.JoinedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (memberships.Count == 0)
        {
            return Array.Empty<OrgAdminSummaryDto>();
        }

        var userIds = memberships.Select(m => m.UserId).Distinct().ToList();

        var profilesList = await _dbContext.IndividualProfiles
            .Where(p => userIds.Contains(p.UserId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var profiles = profilesList.ToDictionary(p => p.UserId, p => p);
        var userDetails = await _identityService.GetUserDetailsByIdsAsync(userIds, cancellationToken).ConfigureAwait(false);

        return memberships.Select(m =>
        {
            profiles.TryGetValue(m.UserId, out var profile);
            userDetails.TryGetValue(m.UserId, out var details);

            return new OrgAdminSummaryDto(
                MembershipId: m.Id,
                UserId: m.UserId,
                FirstName: profile?.FirstName,
                LastName: profile?.LastName,
                Email: details.Email,
                PhoneNumber: details.PhoneNumber,
                Role: m.Role.ToString(),
                Status: m.Status.ToString(),
                JoinedAtUtc: m.JoinedAtUtc);
        }).ToList();
    }
}
