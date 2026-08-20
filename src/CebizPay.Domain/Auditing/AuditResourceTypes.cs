namespace CebizPay.Domain.Auditing;

/// <summary>
/// Authoritative catalog of centralized audit resource types.
/// </summary>
public static class AuditResourceTypes
{
    /// <summary>User resource type.</summary>
    public const string User = "USER";
    /// <summary>Organization resource type.</summary>
    public const string Organization = "ORGANIZATION";
    /// <summary>Organization membership resource type.</summary>
    public const string Membership = "MEMBERSHIP";
    /// <summary>Individual KYC document resource type.</summary>
    public const string KycDocument = "KYC_DOCUMENT";
    /// <summary>Organization KYB application resource type.</summary>
    public const string KybApplication = "KYB_APPLICATION";
    /// <summary>Platform Admin profile resource type.</summary>
    public const string AdminProfile = "ADMIN_PROFILE";
    /// <summary>Platform Fee policy resource type.</summary>
    public const string FeePolicy = "FEE_POLICY";
    /// <summary>Wallet resource type.</summary>
    public const string Wallet = "WALLET";
    /// <summary>Ledger transaction resource type.</summary>
    public const string LedgerTransaction = "LEDGER_TRANSACTION";
    /// <summary>Peer transfer resource type.</summary>
    public const string PeerTransfer = "PEER_TRANSFER";
    /// <summary>Bank transfer resource type.</summary>
    public const string BankTransfer = "BANK_TRANSFER";
    /// <summary>Bank transfer fee policy resource type.</summary>
    public const string BankTransferFeePolicy = "BANK_TRANSFER_FEE_POLICY";
}
