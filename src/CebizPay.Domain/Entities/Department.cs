namespace CebizPay.Domain.Entities;

/// <summary>
/// Represents an organization department.
/// Owned by an Organization for tenant isolation.
/// </summary>
public class Department
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }
    /// <summary>Owning organization ID.</summary>
    public Guid OrganizationId { get; private set; }
    /// <summary>Department name.</summary>
    public string Name { get; private set; } = string.Empty;
    /// <summary>Optional description.</summary>
    public string? Description { get; private set; }
    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private Department() { } // EF Core

    /// <summary>
    /// Creates a new department.
    /// </summary>
    public Department(Guid organizationId, string name, string? description = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Department Name is required.", nameof(name));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Name = name.Trim();
        Description = description?.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates department name and description.
    /// </summary>
    public void Update(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Department Name is required.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim();
    }
}
