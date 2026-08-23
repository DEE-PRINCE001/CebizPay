using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Erp.Entities;

/// <summary>
/// Domain aggregate root representing an organization billable service offering.
/// </summary>
public sealed class ErpService
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Organization identifier for tenant isolation.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Service code, unique per organization (e.g., SVC-001).</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Service name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Service description.</summary>
    public string? Description { get; private set; }

    /// <summary>Standard unit charge/price.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Currency code for billing.</summary>
    public Currency Currency { get; private set; } = Currency.NGN;

    /// <summary>Lifecycle status of the service.</summary>
    public ErpServiceStatus Status { get; private set; } = ErpServiceStatus.Active;

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last update timestamp in UTC.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Soft delete flag.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Soft deleted timestamp.</summary>
    public DateTime? DeletedAtUtc { get; private set; }

    private ErpService() { } // EF Core

    /// <summary>
    /// Initializes a new instance of <see cref="ErpService"/>.
    /// </summary>
    public ErpService(
        Guid organizationId,
        string code,
        string name,
        decimal unitPrice,
        string? description = null,
        Currency currency = Currency.NGN)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Service code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Service name is required.", nameof(name));
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UnitPrice = unitPrice;
        Currency = currency;
        Status = ErpServiceStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates service metadata.
    /// </summary>
    public void Update(string name, string? description, decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Service name is required.", nameof(name));
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UnitPrice = unitPrice;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Deactivates the service.</summary>
    public void Deactivate()
    {
        Status = ErpServiceStatus.Inactive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Activates the service.</summary>
    public void Activate()
    {
        Status = ErpServiceStatus.Active;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Performs soft deletion.</summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
        Status = ErpServiceStatus.Inactive;
    }
}
