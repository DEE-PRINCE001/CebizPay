namespace CebizPay.Domain.Payroll.Enums;

/// <summary>
/// Workforce selection modes for payroll execution.
/// </summary>
public enum PayrollSelectionMode
{
    /// <summary>All eligible active employees in the organization.</summary>
    All = 1,

    /// <summary>Filtered by department IDs.</summary>
    Department = 2,

    /// <summary>Filtered by workforce role IDs.</summary>
    Role = 3,

    /// <summary>Filtered by salary level IDs.</summary>
    Level = 4,

    /// <summary>Filtered by specific individual employee user IDs.</summary>
    Individual = 5
}
