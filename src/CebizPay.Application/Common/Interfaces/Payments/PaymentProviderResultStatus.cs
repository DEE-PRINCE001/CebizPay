namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Classification of a payment provider response outcome.
/// Essential for gateway failover decision-making.
/// </summary>
public enum PaymentProviderResultStatus
{
    /// <summary>Operation confirmed successful by the provider. Never retry or fail over.</summary>
    Success = 1,

    /// <summary>Operation rejected due to business rules (e.g. invalid account, insufficient provider balance). Do not automatically switch provider.</summary>
    BusinessFailure = 2,

    /// <summary>Operation failed due to technical / infrastructure error (e.g. 503 Service Unavailable, network timeout on connect). Fallback / retry may be allowed.</summary>
    TechnicalFailure = 3,

    /// <summary>Operation outcome indeterminate (e.g. timeout after dispatch / connection reset). Reconcile before retry or fallback.</summary>
    Unknown = 4
}
