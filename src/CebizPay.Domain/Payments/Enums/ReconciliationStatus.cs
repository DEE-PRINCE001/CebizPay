#pragma warning disable CS1591
namespace CebizPay.Domain.Payments.Enums;

/// <summary>
/// Lifecycle state of a reconciliation workflow between internal operations and external provider rails.
/// </summary>
public enum ReconciliationStatus
{
    /// <summary>Reconciliation not required (operation finalized synchronously or via standard webhook).</summary>
    NotRequired = 0,

    /// <summary>Reconciliation scheduled or awaiting next provider query.</summary>
    Pending = 1,

    /// <summary>Reconciliation actively in progress / claimed by worker.</summary>
    InProgress = 2,

    /// <summary>Definitively resolved as successful external execution and settled internally.</summary>
    ResolvedSuccess = 3,

    /// <summary>Definitively resolved as failed external execution; internal state marked failed safely.</summary>
    ResolvedFailure = 4,

    /// <summary>Resolved through refund or reversal settlement.</summary>
    ResolvedReversed = 5,

    /// <summary>External outcome remains ambiguous after bounded retry attempts.</summary>
    Unresolved = 6,

    /// <summary>Discrepancy (e.g. amount/currency mismatch, contradictory statuses) escalated for compliance/ops review.</summary>
    ManualReview = 7,

    /// <summary>Reconciliation failed permanently due to irrecoverable technical or data error.</summary>
    FailedPermanently = 8
}

/// <summary>
/// Classifies the financial or compliance operation undergoing reconciliation.
/// </summary>
public enum ReconciliationType
{
    /// <summary>Outbound payment attempt on bank transfer / disbursement rail.</summary>
    PaymentAttempt = 1,

    /// <summary>Parent bank transfer payout aggregate.</summary>
    BankTransfer = 2,

    /// <summary>Inbound wallet funding via virtual accounts or reserved accounts.</summary>
    InboundFunding = 3,

    /// <summary>Inbound card funding payment.</summary>
    CardFunding = 4,

    /// <summary>Card payment refund or chargeback reversal.</summary>
    CardRefund = 5,

    /// <summary>Value-added service (airtime/data) purchase.</summary>
    VasPurchase = 6,

    /// <summary>External KYC/KYB identity verification operation.</summary>
    ComplianceVerification = 7
}
