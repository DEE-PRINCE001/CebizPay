namespace CebizPay.Domain.Payments.Enums;

/// <summary>
/// Lifecycle status of a tokenized saved card.
/// </summary>
public enum SavedCardStatus
{
    /// <summary>Active and valid for recurring/one-click charges.</summary>
    Active = 1,

    /// <summary>Explicitly revoked or deleted by customer.</summary>
    Revoked = 2,

    /// <summary>Card has passed its expiration date.</summary>
    Expired = 3,

    /// <summary>Provider returned token invalidation or authorization failure.</summary>
    Invalid = 4
}
