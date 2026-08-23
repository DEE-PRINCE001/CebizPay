using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Entities;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Staff;

/// <summary>
/// Query to retrieve detailed staff profile for an organization membership.
/// </summary>
public sealed record GetStaffProfileQuery(
    Guid OrganizationId,
    Guid MembershipId) : IRequest<StaffProfileDto?>;

/// <summary>
/// Validator for GetStaffProfileQuery.
/// </summary>
public sealed class GetStaffProfileQueryValidator : AbstractValidator<GetStaffProfileQuery>
{
    /// <summary>
    /// Initializes validation rules for GetStaffProfileQuery.
    /// </summary>
    public GetStaffProfileQueryValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.MembershipId).NotEmpty().WithMessage("MembershipId is required.");
    }
}

/// <summary>
/// Handler for GetStaffProfileQuery.
/// </summary>
public sealed class GetStaffProfileQueryHandler : IRequestHandler<GetStaffProfileQuery, StaffProfileDto?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly IIdentityService _identityService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetStaffProfileQueryHandler"/>.
    /// </summary>
    public GetStaffProfileQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        IIdentityService identityService)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _identityService = identityService;
    }

    /// <inheritdoc/>
    public async Task<StaffProfileDto?> Handle(GetStaffProfileQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var membership = await _dbContext.OrganizationMemberships.FirstOrDefaultAsync(
            m => m.Id == request.MembershipId && m.OrganizationId == request.OrganizationId,
            cancellationToken);

        if (membership == null)
        {
            return null;
        }

        var profile = await _dbContext.IndividualProfiles.FirstOrDefaultAsync(
            p => p.UserId == membership.UserId,
            cancellationToken);

        var userDetails = await _identityService.GetUserDetailsByIdsAsync([membership.UserId], cancellationToken);
        userDetails.TryGetValue(membership.UserId, out var resolvedUser);

        string? deptName = null;
        if (membership.DepartmentId.HasValue)
        {
            var dept = await _dbContext.Departments.FirstOrDefaultAsync(
                d => d.Id == membership.DepartmentId.Value && d.OrganizationId == request.OrganizationId,
                cancellationToken);
            deptName = dept?.Name;
        }

        string? roleTitle = null;
        if (membership.WorkforceRoleId.HasValue)
        {
            var role = await _dbContext.WorkforceRoles.FirstOrDefaultAsync(
                r => r.Id == membership.WorkforceRoleId.Value && r.OrganizationId == request.OrganizationId,
                cancellationToken);
            roleTitle = role?.Title;
        }

        SalaryLevel? salLevel = null;
        if (membership.SalaryLevelId.HasValue)
        {
            salLevel = await _dbContext.SalaryLevels.FirstOrDefaultAsync(
                s => s.Id == membership.SalaryLevelId.Value && s.OrganizationId == request.OrganizationId,
                cancellationToken);
        }

        return new StaffProfileDto(
            membership.Id,
            membership.UserId,
            membership.OrganizationId,
            profile?.FirstName,
            profile?.LastName,
            profile?.MiddleName,
            resolvedUser.Email,
            resolvedUser.PhoneNumber,
            profile?.KycStatus.ToString(),
            profile?.ProfessionalStatus.ToString(),
            membership.DepartmentId,
            deptName,
            membership.WorkforceRoleId,
            roleTitle,
            membership.SalaryLevelId,
            salLevel?.LevelName,
            salLevel?.BaseAmount,
            salLevel?.Currency,
            membership.Role.ToString(),
            membership.Status.ToString(),
            membership.JoinedAtUtc,
            membership.SuspendedAtUtc,
            membership.SuspensionReason);
    }
}
