namespace CebizPay.Domain.Compliance.Enums;

/// <summary>
/// Supported external and internal KYC/KYB identity and business verification providers.
/// </summary>
public enum VerificationProvider
{
    /// <summary>Dojah identity, verification, and business intelligence platform.</summary>
    Dojah = 1,

    /// <summary>Smile ID pan-African identity verification, document OCR, and biometric matching.</summary>
    SmileId = 2,

    /// <summary>Ninja compliance and identity verification provider.</summary>
    Ninja = 3,

    /// <summary>Future internal CebizPay core banking / MFB verification rail.</summary>
    Internal = 4
}
