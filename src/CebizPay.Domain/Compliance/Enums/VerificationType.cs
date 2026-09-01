namespace CebizPay.Domain.Compliance.Enums;

/// <summary>
/// Scope of verification subject: natural person (Individual KYC) vs legal entity / legal arrangement (Organization KYB).
/// In accordance with CBN Customer Due Diligence regulations, individual tiered KYC must never be applied to legal entities.
/// </summary>
public enum VerificationType
{
    /// <summary>Natural person individual identity verification (KYC).</summary>
    IndividualKyc = 1,

    /// <summary>Legal person / organization corporate verification (KYB).</summary>
    OrganizationKyb = 2
}
