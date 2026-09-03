namespace CebizPay.Domain.Communication.Enums;

/// <summary>
/// Domain enumeration representing supported notification categories across CebizPay bounded contexts.
/// </summary>
public enum NotificationType
{
    /// <summary>Emitted when an organization is suspended or compliance-restricted.</summary>
    OrganizationSuspended = 1,

    /// <summary>Emitted when a corporate or staff loan application is approved.</summary>
    LoanApproved = 2,

    /// <summary>Emitted when a payroll batch disbursement completes successfully.</summary>
    PayrollCompleted = 3,

    /// <summary>Emitted when a thrift contribution is missed or a member cycle becomes delinquent.</summary>
    ThriftDelinquency = 4,

    /// <summary>Emitted when a global platform announcement is officially published.</summary>
    PlatformAnnouncement = 5,

    /// <summary>Emitted when a tenant-scoped workplace announcement is officially published.</summary>
    WorkplaceAnnouncement = 6,

    /// <summary>Emitted for critical security-sensitive events (MFA challenges, admin invitations, password/PIN updates).</summary>
    SecurityAlert = 7
}
