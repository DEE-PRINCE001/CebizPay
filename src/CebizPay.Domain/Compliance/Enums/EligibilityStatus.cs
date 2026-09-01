namespace CebizPay.Domain.Compliance.Enums;

/// <summary>
/// Status outcome of a transaction compliance eligibility evaluation.
/// </summary>
public enum EligibilityStatus
{
    /// <summary>Transaction is fully allowed to proceed to financial processing.</summary>
    Allowed = 1,
    /// <summary>Transaction is blocked due to active compliance restrictions or limits.</summary>
    Restricted = 2,
    /// <summary>Transaction requires manual compliance officer review before execution.</summary>
    ReviewRequired = 3,
    /// <summary>Transaction is blocked pending completion of Enhanced Due Diligence (EDD).</summary>
    EddRequired = 4,
    /// <summary>Transaction is strictly blocked due to subject suspension or sanctions.</summary>
    Suspended = 5
}
