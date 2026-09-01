namespace CebizPay.Domain.Compliance.Enums;

/// <summary>
/// Authoritative compliance decision produced by the CebizPay Compliance Decision Engine.
/// </summary>
public enum ComplianceDecisionType
{
    /// <summary>Account / relationship approved for standard financial operations.</summary>
    Approved = 1,
    /// <summary>Account placed in review requiring manual compliance sign-off.</summary>
    ReviewRequired = 2,
    /// <summary>Account requires completion of Enhanced Due Diligence (EDD) workflow.</summary>
    EddRequired = 3,
    /// <summary>Account restricted with specific operational or volume caps.</summary>
    Restricted = 4,
    /// <summary>Account rejected for compliance or regulatory failure.</summary>
    Rejected = 5,
    /// <summary>Account suspended / frozen pending formal regulatory or legal investigation.</summary>
    Suspended = 6
}
