namespace CebizPay.Domain.Compliance.Enums;

/// <summary>
/// Distinct capabilities supported across compliance verification providers.
/// </summary>
public enum VerificationCapability
{
    /// <summary>Individual identity verification via BVN or NIN.</summary>
    Identity = 1,

    /// <summary>Biometric liveness detection and 1:1 facial biometric matching.</summary>
    Biometrics = 2,

    /// <summary>Government-issued identity document OCR and validation (NIMC, Passport, Driver's License, Voter's Card).</summary>
    Document = 3,

    /// <summary>Anti-Money Laundering (AML), Politically Exposed Persons (PEP), and sanctions watchlist screening.</summary>
    AmlScreening = 4,

    /// <summary>Corporate Affairs Commission (CAC) business registry lookup and director verification.</summary>
    Business = 5,

    /// <summary>Beneficial ownership and shareholder verification.</summary>
    BeneficialOwnership = 6
}
