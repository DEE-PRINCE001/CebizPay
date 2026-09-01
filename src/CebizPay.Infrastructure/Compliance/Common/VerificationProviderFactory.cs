#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Infrastructure.Compliance.Common;

/// <summary>
/// Factory resolving capability-specific provider implementations based on <see cref="VerificationProvider"/> enum.
/// </summary>
public sealed class VerificationProviderFactory : IVerificationProviderFactory
{
    private readonly IEnumerable<IIdentityVerificationProvider> _identityProviders;
    private readonly IEnumerable<IBiometricVerificationProvider> _biometricProviders;
    private readonly IEnumerable<IDocumentVerificationProvider> _documentProviders;
    private readonly IEnumerable<IAmlScreeningProvider> _amlProviders;
    private readonly IEnumerable<IBusinessVerificationProvider> _businessProviders;

    public VerificationProviderFactory(
        IEnumerable<IIdentityVerificationProvider> identityProviders,
        IEnumerable<IBiometricVerificationProvider> biometricProviders,
        IEnumerable<IDocumentVerificationProvider> documentProviders,
        IEnumerable<IAmlScreeningProvider> amlProviders,
        IEnumerable<IBusinessVerificationProvider> businessProviders)
    {
        _identityProviders = identityProviders ?? Enumerable.Empty<IIdentityVerificationProvider>();
        _biometricProviders = biometricProviders ?? Enumerable.Empty<IBiometricVerificationProvider>();
        _documentProviders = documentProviders ?? Enumerable.Empty<IDocumentVerificationProvider>();
        _amlProviders = amlProviders ?? Enumerable.Empty<IAmlScreeningProvider>();
        _businessProviders = businessProviders ?? Enumerable.Empty<IBusinessVerificationProvider>();
    }

    public IIdentityVerificationProvider GetIdentityVerificationProvider(VerificationProvider provider) =>
        _identityProviders.FirstOrDefault(p => p.Provider == provider)
        ?? throw new NotSupportedException($"Identity verification provider '{provider}' is not registered.");

    public IBiometricVerificationProvider GetBiometricVerificationProvider(VerificationProvider provider) =>
        _biometricProviders.FirstOrDefault(p => p.Provider == provider)
        ?? throw new NotSupportedException($"Biometric verification provider '{provider}' is not registered.");

    public IDocumentVerificationProvider GetDocumentVerificationProvider(VerificationProvider provider) =>
        _documentProviders.FirstOrDefault(p => p.Provider == provider)
        ?? throw new NotSupportedException($"Document verification provider '{provider}' is not registered.");

    public IAmlScreeningProvider GetAmlScreeningProvider(VerificationProvider provider) =>
        _amlProviders.FirstOrDefault(p => p.Provider == provider)
        ?? throw new NotSupportedException($"AML screening provider '{provider}' is not registered.");

    public IBusinessVerificationProvider GetBusinessVerificationProvider(VerificationProvider provider) =>
        _businessProviders.FirstOrDefault(p => p.Provider == provider)
        ?? throw new NotSupportedException($"Business verification provider '{provider}' is not registered.");
}
