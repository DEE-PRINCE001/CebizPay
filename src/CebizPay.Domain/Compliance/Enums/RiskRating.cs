namespace CebizPay.Domain.Compliance.Enums;

/// <summary>
/// Authoritative risk rating assigned by the CebizPay Risk Engine.
/// In accordance with CBN Customer Due Diligence regulations.
/// </summary>
public enum RiskRating
{
    /// <summary>Low risk subject (eligible for Simplified / Basic CDD).</summary>
    Low = 1,
    /// <summary>Medium / Standard risk subject (Standard CDD).</summary>
    Medium = 2,
    /// <summary>High risk subject (mandatory Enhanced Due Diligence and enhanced monitoring).</summary>
    High = 3,
    /// <summary>Prohibited / Sanctioned subject (immediate compliance hold, no financial activity).</summary>
    Prohibited = 4
}
