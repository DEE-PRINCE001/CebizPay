using CebizPay.Domain.Erp.Enums;

namespace CebizPay.Domain.Erp.Entities;

/// <summary>
/// Domain aggregate root representing a B2B or B2C customer of an organization.
/// </summary>
public sealed class Customer
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Organization identifier for tenant isolation.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Human-readable unique customer reference (e.g., CUST-001).</summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>Customer individual or company name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Primary contact email address.</summary>
    public string? Email { get; private set; }

    /// <summary>Primary contact telephone number.</summary>
    public string? Phone { get; private set; }

    /// <summary>Billing or delivery address.</summary>
    public string? Address { get; private set; }

    /// <summary>Lifecycle status of the customer.</summary>
    public CustomerStatus Status { get; private set; } = CustomerStatus.Active;

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last update timestamp in UTC.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Soft delete flag.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Soft deleted timestamp.</summary>
    public DateTime? DeletedAtUtc { get; private set; }

    private Customer() { } // EF Core

    /// <summary>
    /// Initializes a new instance of <see cref="Customer"/>.
    /// </summary>
    public Customer(
        Guid organizationId,
        string reference,
        string name,
        string? email = null,
        string? phone = null,
        string? address = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("Customer reference is required.", nameof(reference));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Customer name is required.", nameof(name));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Reference = reference.Trim().ToUpperInvariant();
        Name = name.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        Status = CustomerStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates customer details.
    /// </summary>
    public void Update(
        string name,
        string? email,
        string? phone,
        string? address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Customer name is required.", nameof(name));
        }

        Name = name.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Deactivates the customer.</summary>
    public void Deactivate()
    {
        Status = CustomerStatus.Inactive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Activates the customer.</summary>
    public void Activate()
    {
        Status = CustomerStatus.Active;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Performs soft deletion.</summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
        Status = CustomerStatus.Inactive;
    }
}
