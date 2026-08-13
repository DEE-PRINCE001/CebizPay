namespace CebizPay.Application.Common.Interfaces.Security;

/// <summary>
/// Provides tenant context and isolation checks for the current authenticated request.
/// </summary>
public interface ICurrentOrganizationContext
{
    /// <summary>
    /// Gets the currently active OrganizationId from HTTP headers / claims context, if set.
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
}
