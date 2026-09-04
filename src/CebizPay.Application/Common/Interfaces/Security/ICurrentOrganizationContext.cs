using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;

namespace CebizPay.Application.Common.Interfaces.Security;

/// <summary>
/// Provides tenant context and isolation checks for the current authenticated request.
/// Client-supplied organization identifiers (e.g. via X-Organization-Id header) are selection
/// hints only and are never treated as authorization authority. The server verifies authenticated
/// user membership and authorization before establishing organization context.
/// </summary>
public interface ICurrentOrganizationContext
{
    /// <summary>
    /// Gets the currently active OrganizationId from HTTP headers / claims context, validated against server-side authorization.
    /// Returns null if no organization was requested or if the authenticated user is not authorized for the requested organization.
    /// </summary>
    Guid? CurrentOrganizationId { get; }

    /// <summary>
    /// Gets a value indicating whether an active organization context is present.
    /// </summary>
    bool IsInOrganizationContext { get; }

    /// <summary>
    /// Validates that the authenticated user is an active member of the specified organization ID.
    /// </summary>
    /// <param name="organizationId">Organization ID to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if authorized; otherwise false.</returns>
    Task<bool> HasAccessToOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the active organization membership for the current authenticated user.
    /// </summary>
    Task<OrganizationMembership?> GetCurrentMembershipAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the authenticated user has a specific organization permission (or is SuperAdmin).
    /// </summary>
    Task<bool> HasPermissionAsync(Guid organizationId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the authenticated user holds one of the specified membership roles (or is SuperAdmin).
    /// </summary>
    Task<bool> HasRoleAsync(Guid organizationId, MembershipRoleType[] allowedRoles, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the authenticated user is an active platform SuperAdmin.
    /// </summary>
    Task<bool> IsSuperAdminAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the active platform AdminProfile for the authenticated user, or null if none.
    /// </summary>
    Task<AdminProfile?> GetCurrentAdminProfileAsync(CancellationToken cancellationToken = default);
}

