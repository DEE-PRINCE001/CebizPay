using CebizPay.Domain.Erp.Enums;

namespace CebizPay.Domain.Erp.Entities;

/// <summary>
/// Domain entity representing an organization's active or historical inventory valuation policy (WAC / FIFO).
/// </summary>
public sealed class InventoryValuationPolicy
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Organization identifier for tenant isolation.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Valuation method (WAC / FIFO).</summary>
    public ValuationMethod Method { get; private set; }

    /// <summary>Monotonically increasing version number for the organization.</summary>
    public int Version { get; private set; }

    /// <summary>Timestamp from which this policy version is effective.</summary>
    public DateTime EffectiveFromUtc { get; private set; }

    /// <summary>Timestamp at which this policy version was deactivated (null if active).</summary>
    public DateTime? DeactivatedAtUtc { get; private set; }

    /// <summary>Flag indicating whether this is the currently active valuation policy for the organization.</summary>
    public bool IsActive { get; private set; }

    /// <summary>User ID of the actor who created/activated this policy version.</summary>
    public string CreatedByUserId { get; private set; } = string.Empty;

    /// <summary>Timestamp when this policy was recorded.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private InventoryValuationPolicy() { } // EF Core

    /// <summary>
    /// Initializes a new instance of <see cref="InventoryValuationPolicy"/>.
    /// </summary>
    public InventoryValuationPolicy(
        Guid organizationId,
        ValuationMethod method,
        int version,
        string createdByUserId,
        DateTime effectiveFromUtc,
        bool isActive = true)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));
        }

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Policy version must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new ArgumentException("CreatedByUserId cannot be empty.", nameof(createdByUserId));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Method = method;
        Version = version;
        EffectiveFromUtc = effectiveFromUtc;
        IsActive = isActive;
        CreatedByUserId = createdByUserId.Trim();
        CreatedAtUtc = effectiveFromUtc;
    }

    /// <summary>
    /// Deactivates this valuation policy version when a new policy is activated.
    /// </summary>
    public void Deactivate(DateTime deactivatedAtUtc)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        DeactivatedAtUtc = deactivatedAtUtc;
    }

    /// <summary>
    /// Factory to create the default initial WAC policy (Version 1) for an organization.
    /// </summary>
    public static InventoryValuationPolicy CreateInitialDefault(
        Guid organizationId,
        string createdByUserId,
        DateTime utcNow)
    {
        return new InventoryValuationPolicy(
            organizationId,
            ValuationMethod.Wac,
            version: 1,
            createdByUserId,
            utcNow,
            isActive: true);
    }

    /// <summary>
    /// Factory to create a subsequent policy version when the organization changes valuation method.
    /// </summary>
    public static InventoryValuationPolicy CreateNextVersion(
        Guid organizationId,
        ValuationMethod newMethod,
        int nextVersion,
        string createdByUserId,
        DateTime utcNow)
    {
        return new InventoryValuationPolicy(
            organizationId,
            newMethod,
            version: nextVersion,
            createdByUserId,
            utcNow,
            isActive: true);
    }
}
