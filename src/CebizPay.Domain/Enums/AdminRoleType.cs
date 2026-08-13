namespace CebizPay.Domain.Enums;

/// <summary>
/// Administrative user role classification.
/// </summary>
public enum AdminRoleType
{
    /// <summary>Super admin role.</summary>
    SuperAdmin = 1,
    /// <summary>Standard admin role.</summary>
    Admin = 2,
    /// <summary>Auditor role with read-only access.</summary>
    Auditor = 3
}
