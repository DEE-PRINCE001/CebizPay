namespace CebizPay.Domain.Enums;

/// <summary>
/// Supported KYC document types.
/// </summary>
public enum DocumentType
{
    /// <summary>NIMC slip or card.</summary>
    Nimc = 1,
    /// <summary>Driver's license.</summary>
    DriversLicense = 2,
    /// <summary>International passport.</summary>
    InternationalPassport = 3,
    /// <summary>Liveness selfie photo.</summary>
    Liveness = 4
}
