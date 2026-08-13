using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;

namespace CebizPay.Domain.Entities;

/// <summary>
/// Domain representation for administrative users.
/// Supports SuperAdmin, Admin, and Read-Only Admin / Auditor roles, with granular delegated permissions and MFA state.
/// </summary>
public class AdminProfile
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }
    /// <summary>User ID matching Identity ApplicationUser Id.</summary>
    public string UserId { get; private set; } = string.Empty;
    /// <summary>Administrative role type.</summary>
    public AdminRoleType Role { get; private set; }
    /// <summary>Active state flag.</summary>
    public bool IsActive { get; private set; } = true;
    /// <summary>MFA enabled flag for this web admin profile.</summary>
    public bool IsMfaEnabled { get; private set; }
    /// <summary>Granular delegated permissions granted to this admin.</summary>
    public List<string> PermissionsList { get; private set; } = new();
    /// <summary>Created timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }
    /// <summary>Updated timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    private AdminProfile() { } // EF Core

    /// <summary>
    /// Creates a new admin profile.
    /// </summary>
    public AdminProfile(string userId, AdminRoleType role, bool isMfaEnabled = false)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));

        Id = Guid.NewGuid();
        UserId = userId;
        Role = role;
        IsActive = true;
        IsMfaEnabled = isMfaEnabled;
        CreatedAtUtc = DateTime.UtcNow;

        // Automatically assign default read-only permissions if Auditor role
        if (role == AdminRoleType.Auditor)
        {
            PermissionsList.AddRange(Permissions.Permissions.ReadOnlyAdminPermissions);
        }
    }

    /// <summary>
    /// Changes the administrative role.
    /// </summary>
    public void ChangeRole(AdminRoleType newRole)
    {
        Role = newRole;
        if (newRole == AdminRoleType.Auditor)
        {
            PermissionsList = Permissions.Permissions.ReadOnlyAdminPermissions.ToList();
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Grants a delegated permission to this admin profile.
    /// </summary>
    public void GrantPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
            throw new ArgumentException("Permission is required.", nameof(permission));

        var trimmed = permission.Trim();
        if (!PermissionsList.Contains(trimmed))
        {
            PermissionsList.Add(trimmed);
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Revokes a delegated permission from this admin profile.
    /// </summary>
    public void RevokePermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
            throw new ArgumentException("Permission is required.", nameof(permission));

        var trimmed = permission.Trim();
        if (PermissionsList.Remove(trimmed))
        {
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Checks whether this admin profile possesses a specific permission.
    /// </summary>
    public bool HasPermission(string permission)
    {
        if (Role == AdminRoleType.SuperAdmin) return true;
        if (string.IsNullOrWhiteSpace(permission)) return false;
        return PermissionsList.Contains(permission.Trim());
    }

    /// <summary>
    /// Toggles the MFA enabled status for this admin profile.
    /// </summary>
    public void SetMfaStatus(bool enabled)
    {
        IsMfaEnabled = enabled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the admin profile.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Activates the admin profile.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
