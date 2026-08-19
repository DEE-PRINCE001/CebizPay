namespace CebizPay.Domain.Auditing;

/// <summary>
/// Authoritative catalog of centralized audit action names.
/// </summary>
public static class AuditActions
{
    // Auth & Identity & Security
    /// <summary>Audit action when a new user registers.</summary>
    public const string UserRegistered = "USER_REGISTERED";
    /// <summary>Audit action when a user password is changed.</summary>
    public const string PasswordChanged = "PASSWORD_CHANGED";
    /// <summary>Audit action when MFA is enabled.</summary>
    public const string MfaEnabled = "MFA_ENABLED";
    /// <summary>Audit action when MFA is disabled.</summary>
    public const string MfaDisabled = "MFA_DISABLED";
    /// <summary>Audit action when a transaction PIN is set.</summary>
    public const string PinSet = "PIN_SET";
    /// <summary>Audit action when a transaction PIN is changed.</summary>
    public const string PinChanged = "PIN_CHANGED";
    /// <summary>Audit action when a transaction PIN is locked due to failed attempts.</summary>
    public const string PinLocked = "PIN_LOCKED";

    // KYC / Individual Compliance
    /// <summary>Audit action when individual KYC is submitted.</summary>
    public const string KycSubmitted = "KYC_SUBMITTED";
    /// <summary>Audit action when individual KYC is verified.</summary>
    public const string KycVerified = "KYC_VERIFIED";
    /// <summary>Audit action when individual KYC is rejected.</summary>
    public const string KycRejected = "KYC_REJECTED";

    // KYB / Organization Compliance
    /// <summary>Audit action when organization KYB is submitted.</summary>
    public const string KybSubmitted = "KYB_SUBMITTED";
    /// <summary>Audit action when organization KYB is verified.</summary>
    public const string KybVerified = "KYB_VERIFIED";
    /// <summary>Audit action when organization KYB is rejected.</summary>
    public const string KybRejected = "KYB_REJECTED";

    // Organization Lifecycle & Memberships
    /// <summary>Audit action when an organization is suspended.</summary>
    public const string OrganizationSuspended = "ORGANIZATION_SUSPENDED";
    /// <summary>Audit action when an organization is reactivated.</summary>
    public const string OrganizationReactivated = "ORGANIZATION_REACTIVATED";
    /// <summary>Audit action when an organization membership is suspended.</summary>
    public const string MembershipSuspended = "MEMBERSHIP_SUSPENDED";
    /// <summary>Audit action when an organization membership is reactivated.</summary>
    public const string MembershipReactivated = "MEMBERSHIP_REACTIVATED";

    // Platform Administration & Permissions
    /// <summary>Audit action when an admin permission is granted.</summary>
    public const string AdminPermissionGranted = "ADMIN_PERMISSION_GRANTED";
    /// <summary>Audit action when an admin permission is revoked.</summary>
    public const string AdminPermissionRevoked = "ADMIN_PERMISSION_REVOKED";
    /// <summary>Audit action when an admin status changes.</summary>
    public const string AdminStatusChanged = "ADMIN_STATUS_CHANGED";
    /// <summary>Audit action when an admin is invited.</summary>
    public const string AdminInvited = "ADMIN_INVITED";
    /// <summary>Audit action when an admin is deleted.</summary>
    public const string AdminDeleted = "ADMIN_DELETED";

    // Fee Policies
    /// <summary>Audit action when a fee policy is created.</summary>
    public const string FeePolicyCreated = "FEE_POLICY_CREATED";
    /// <summary>Audit action when a fee policy is activated.</summary>
    public const string FeePolicyActivated = "FEE_POLICY_ACTIVATED";
    /// <summary>Audit action when a fee policy is deactivated.</summary>
    public const string FeePolicyDeactivated = "FEE_POLICY_DEACTIVATED";

    // Transfers & Financial Operations
    /// <summary>Audit action when a peer wallet transfer completes.</summary>
    public const string PeerTransferCompleted = "PEER_TRANSFER_COMPLETED";
    /// <summary>Audit action when a peer wallet transfer is reversed.</summary>
    public const string PeerTransferReversed = "PEER_TRANSFER_REVERSED";
}
