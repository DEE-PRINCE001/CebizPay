namespace CebizPay.Domain.Compliance.Enums;

/// <summary>
/// Regulatory Customer Due Diligence (CDD) depth level as defined by CBN Regulations.
/// </summary>
public enum CddLevel
{
    /// <summary>Simplified / Basic CDD applied to low-risk relationships (Tier 1 Individual).</summary>
    Basic = 1,
    /// <summary>Standard CDD applied to standard-risk individuals and verified corporate entities.</summary>
    Standard = 2,
    /// <summary>Enhanced CDD applied to high-risk customers, PEPs, cross-border or complex ownerships.</summary>
    Enhanced = 3
}
