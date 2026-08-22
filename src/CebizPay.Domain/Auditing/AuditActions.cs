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
    /// <summary>Audit action when a bank transfer fee policy is created.</summary>
    public const string BankTransferFeePolicyCreated = "BANK_TRANSFER_FEE_POLICY_CREATED";
    /// <summary>Audit action when a bank transfer fee policy is activated.</summary>
    public const string BankTransferFeePolicyActivated = "BANK_TRANSFER_FEE_POLICY_ACTIVATED";
    /// <summary>Audit action when a bank transfer fee policy is deactivated.</summary>
    public const string BankTransferFeePolicyDeactivated = "BANK_TRANSFER_FEE_POLICY_DEACTIVATED";

    // Transfers & Financial Operations
    /// <summary>Audit action when a peer wallet transfer completes.</summary>
    public const string PeerTransferCompleted = "PEER_TRANSFER_COMPLETED";
    /// <summary>Audit action when a peer wallet transfer is reversed.</summary>
    public const string PeerTransferReversed = "PEER_TRANSFER_REVERSED";
    /// <summary>Audit action when a bank transfer is created (immediate debit committed in PENDING status).</summary>
    public const string BankTransferCreated = "BANK_TRANSFER_CREATED";
    /// <summary>Audit action when a bank transfer is marked processing.</summary>
    public const string BankTransferProcessing = "BANK_TRANSFER_PROCESSING";
    /// <summary>Audit action when a bank transfer is confirmed completed.</summary>
    public const string BankTransferCompleted = "BANK_TRANSFER_COMPLETED";
    /// <summary>Audit action when a bank transfer definitively fails.</summary>
    public const string BankTransferFailed = "BANK_TRANSFER_FAILED";
    /// <summary>Audit action when a bank transfer status is marked unknown.</summary>
    public const string BankTransferUnknown = "BANK_TRANSFER_UNKNOWN";
    /// <summary>Audit action when a bank transfer is reversed and funds are refunded to sender.</summary>
    public const string BankTransferReversed = "BANK_TRANSFER_REVERSED";

    // Payments & Provider Attempts
    /// <summary>Audit action when a payment provider attempt is created.</summary>
    public const string PaymentAttemptCreated = "PAYMENT_ATTEMPT_CREATED";
    /// <summary>Audit action when a payment provider attempt begins processing.</summary>
    public const string PaymentAttemptProcessing = "PAYMENT_ATTEMPT_PROCESSING";
    /// <summary>Audit action when a payment provider attempt succeeds.</summary>
    public const string PaymentAttemptSucceeded = "PAYMENT_ATTEMPT_SUCCEEDED";
    /// <summary>Audit action when a payment provider attempt fails.</summary>
    public const string PaymentAttemptFailed = "PAYMENT_ATTEMPT_FAILED";
    /// <summary>Audit action when a payment provider attempt outcome is unknown.</summary>
    public const string PaymentAttemptUnknown = "PAYMENT_ATTEMPT_UNKNOWN";
    /// <summary>Audit action when a payment provider attempt is cancelled.</summary>
    public const string PaymentAttemptCancelled = "PAYMENT_ATTEMPT_CANCELLED";

    // Webhooks & Reconciliation
    /// <summary>Audit action when a provider webhook is received.</summary>
    public const string WebhookReceived = "WEBHOOK_RECEIVED";
    /// <summary>Audit action when a provider webhook is processed.</summary>
    public const string WebhookProcessed = "WEBHOOK_PROCESSED";
    /// <summary>Audit action when a provider webhook is rejected (invalid signature/payload).</summary>
    public const string WebhookRejected = "WEBHOOK_REJECTED";
    /// <summary>Audit action when a duplicate provider webhook is safely acknowledged.</summary>
    public const string WebhookDuplicate = "WEBHOOK_DUPLICATE";
    /// <summary>Audit action when a payment attempt is reconciled via webhook or query.</summary>
    public const string PaymentAttemptReconciled = "PAYMENT_ATTEMPT_RECONCILED";

    // Provider Failover
    /// <summary>Audit action when provider failover is initiated.</summary>
    public const string ProviderFailoverInitiated = "PROVIDER_FAILOVER_INITIATED";
    /// <summary>Audit action when provider failover succeeds.</summary>
    public const string ProviderFailoverSucceeded = "PROVIDER_FAILOVER_SUCCEEDED";
    /// <summary>Audit action when provider failover fails.</summary>
    public const string ProviderFailoverFailed = "PROVIDER_FAILOVER_FAILED";

    // Virtual Accounts & Inbound Funding
    /// <summary>Audit action when a dedicated virtual account is provisioned.</summary>
    public const string VirtualAccountCreated = "VIRTUAL_ACCOUNT_CREATED";
    /// <summary>Audit action when a virtual account is activated.</summary>
    public const string VirtualAccountActivated = "VIRTUAL_ACCOUNT_ACTIVATED";
    /// <summary>Audit action when a virtual account is suspended.</summary>
    public const string VirtualAccountSuspended = "VIRTUAL_ACCOUNT_SUSPENDED";
    /// <summary>Audit action when an inbound funding deposit is received and credited.</summary>
    public const string FundingReceived = "FUNDING_RECEIVED";
    /// <summary>Audit action when card funding is initialized.</summary>
    public const string CardFundingInitiated = "CARD_FUNDING_INITIATED";
    /// <summary>Audit action when card funding is completed and credited.</summary>
    public const string CardFundingCompleted = "CARD_FUNDING_COMPLETED";
    /// <summary>Audit action when card funding fails.</summary>
    public const string CardFundingFailed = "CARD_FUNDING_FAILED";

    // Payroll & Payment Vouchers
    /// <summary>Audit action when a payroll batch is created.</summary>
    public const string PayrollCreated = "PAYROLL_CREATED";
    /// <summary>Audit action when payroll execution starts.</summary>
    public const string PayrollStarted = "PAYROLL_STARTED";
    /// <summary>Audit action when a payroll batch is fully completed.</summary>
    public const string PayrollCompleted = "PAYROLL_COMPLETED";
    /// <summary>Audit action when a payroll batch partially completes.</summary>
    public const string PayrollPartiallyCompleted = "PAYROLL_PARTIALLY_COMPLETED";
    /// <summary>Audit action when a payroll batch fails completely.</summary>
    public const string PayrollFailed = "PAYROLL_FAILED";
    /// <summary>Audit action when a payroll batch is cancelled.</summary>
    public const string PayrollCancelled = "PAYROLL_CANCELLED";
    /// <summary>Audit action when a payroll item starts execution.</summary>
    public const string PayrollItemStarted = "PAYROLL_ITEM_STARTED";
    /// <summary>Audit action when a payroll item completes successfully.</summary>
    public const string PayrollItemCompleted = "PAYROLL_ITEM_COMPLETED";
    /// <summary>Audit action when a payroll item fails.</summary>
    public const string PayrollItemFailed = "PAYROLL_ITEM_FAILED";
    /// <summary>Audit action when a payroll item is retried.</summary>
    public const string PayrollItemRetried = "PAYROLL_ITEM_RETRIED";
    /// <summary>Audit action when a payment voucher is created.</summary>
    public const string PaymentVoucherCreated = "PAYMENT_VOUCHER_CREATED";
    /// <summary>Audit action when payment voucher metadata is updated.</summary>
    public const string PaymentVoucherMetadataUpdated = "PAYMENT_VOUCHER_METADATA_UPDATED";
}
