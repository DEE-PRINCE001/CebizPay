using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Factory service for resolving capability-specific provider implementations.
/// </summary>
public interface IVerificationProviderFactory
{
    /// <summary>Resolves the identity verification provider instance.</summary>
    IIdentityVerificationProvider GetIdentityVerificationProvider(VerificationProvider provider);

    /// <summary>Resolves the biometric verification provider instance.</summary>
    IBiometricVerificationProvider GetBiometricVerificationProvider(VerificationProvider provider);

    /// <summary>Resolves the document verification provider instance.</summary>
    IDocumentVerificationProvider GetDocumentVerificationProvider(VerificationProvider provider);

    /// <summary>Resolves the AML/PEP screening provider instance.</summary>
    IAmlScreeningProvider GetAmlScreeningProvider(VerificationProvider provider);

    /// <summary>Resolves the business verification provider instance.</summary>
    IBusinessVerificationProvider GetBusinessVerificationProvider(VerificationProvider provider);
}
