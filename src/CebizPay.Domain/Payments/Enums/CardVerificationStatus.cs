namespace CebizPay.Domain.Payments.Enums;

/// <summary>
/// Status of a card zero-auth or micro-charge verification operation.
/// </summary>
public enum CardVerificationStatus
{
    /// <summary>Verification session initialized with provider.</summary>
    Pending = 1,

    /// <summary>Card successfully authenticated, tokenized, and verified.</summary>
    Verified = 2,

    /// <summary>Verification failed or rejected by issuing bank/provider.</summary>
    Failed = 3,

    /// <summary>Micro-charge verification completed and nominal charge refunded.</summary>
    Refunded = 4
}
