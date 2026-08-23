namespace CebizPay.Domain.Entities;

/// <summary>
/// Represents a job role within an organization.
/// Owned by an Organization for tenant isolation.
/// </summary>
public class WorkforceRole
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }
    /// <summary>Owning organization ID.</summary>
    public Guid OrganizationId { get; private set; }
    /// <summary>Optional department ID.</summary>
    public Guid? DepartmentId { get; private set; }
    /// <summary>Role title.</summary>
    public string Title { get; private set; } = string.Empty;
    /// <summary>Optional description.</summary>
    public string? Description { get; private set; }
    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private WorkforceRole() { } // EF Core

    /// <summary>
    /// Creates a new workforce role.
    /// </summary>
    public WorkforceRole(Guid organizationId, string title, Guid? departmentId = null, string? description = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Role Title is required.", nameof(title));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        DepartmentId = departmentId;
        Title = title.Trim();
        Description = description?.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates workforce role title, department association, and description.
    /// </summary>
    public void Update(string title, Guid? departmentId = null, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Role Title is required.", nameof(title));

        Title = title.Trim();
        DepartmentId = departmentId;
        Description = description?.Trim();
    }
}
