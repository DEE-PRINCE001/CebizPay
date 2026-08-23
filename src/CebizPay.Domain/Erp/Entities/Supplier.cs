using CebizPay.Domain.Erp.Enums;

namespace CebizPay.Domain.Erp.Entities;

/// <summary>
/// Domain aggregate root representing an external vendor/supplier for an organization.
/// </summary>
public sealed class Supplier
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Organization identifier for tenant isolation.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Human-readable unique business reference (e.g., SUP-001).</summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>Vendor/Supplier business or trading name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Primary contact email address.</summary>
    public string? Email { get; private set; }

    /// <summary>Primary contact telephone number.</summary>
    public string? Phone { get; private set; }

    /// <summary>Physical / operating address.</summary>
    public string? Address { get; private set; }

    /// <summary>Tax Identification Number (TIN) / CAC / VAT registration.</summary>
    public string? TaxIdentifier { get; private set; }

    /// <summary>Lifecycle status of the supplier.</summary>
    public SupplierStatus Status { get; private set; } = SupplierStatus.Active;

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last update timestamp in UTC.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Soft delete flag.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Soft deleted timestamp.</summary>
    public DateTime? DeletedAtUtc { get; private set; }

    private Supplier() { } // EF Core

    /// <summary>
    /// Initializes a new instance of <see cref="Supplier"/>.
    /// </summary>
    public Supplier(
        Guid organizationId,
        string reference,
        string name,
        string? email = null,
        string? phone = null,
        string? address = null,
        string? taxIdentifier = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("Supplier reference is required.", nameof(reference));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Supplier name is required.", nameof(name));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Reference = reference.Trim().ToUpperInvariant();
        Name = name.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        TaxIdentifier = string.IsNullOrWhiteSpace(taxIdentifier) ? null : taxIdentifier.Trim();
        Status = SupplierStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates supplier details.
    /// </summary>
    public void Update(
        string name,
        string? email,
        string? phone,
        string? address,
        string? taxIdentifier)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Supplier name is required.", nameof(name));
        }

        Name = name.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        TaxIdentifier = string.IsNullOrWhiteSpace(taxIdentifier) ? null : taxIdentifier.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Deactivates the supplier.</summary>
    public void Deactivate()
    {
        Status = SupplierStatus.Inactive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Activates the supplier.</summary>
    public void Activate()
    {
        Status = SupplierStatus.Active;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Performs soft deletion.</summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
        Status = SupplierStatus.Inactive;
    }
}
