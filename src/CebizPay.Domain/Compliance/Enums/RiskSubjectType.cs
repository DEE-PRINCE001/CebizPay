namespace CebizPay.Domain.Compliance.Enums;

/// <summary>
/// Target subject of a risk assessment or compliance decision.
/// </summary>
public enum RiskSubjectType
{
    /// <summary>Natural person / Individual customer.</summary>
    Individual = 1,
    /// <summary>Legal entity / Corporate organization.</summary>
    Organization = 2,
    /// <summary>Specific discrete transaction instance.</summary>
    Transaction = 3
}
