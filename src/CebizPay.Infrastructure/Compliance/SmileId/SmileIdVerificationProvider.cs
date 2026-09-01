#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Enums;

namespace CebizPay.Infrastructure.Compliance.SmileId;

/// <summary>
/// Smile ID provider adapter implementing compliance verification capabilities.
/// </summary>
public sealed class SmileIdVerificationProvider :
    IIdentityVerificationProvider,
    IBiometricVerificationProvider,
    IDocumentVerificationProvider,
    IAmlScreeningProvider,
    IBusinessVerificationProvider,
    IVerificationProvider
{
    private readonly ISmileIdClient _client;

    public VerificationProvider Provider => VerificationProvider.SmileId;

    public SmileIdVerificationProvider(ISmileIdClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public Task<VerificationProviderResult> VerifyBvnAsync(
        string bvn,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        CancellationToken cancellationToken = default) =>
        _client.VerifyBvnAsync(bvn, firstName, lastName, dateOfBirth, cancellationToken);

    public Task<VerificationProviderResult> VerifyNinAsync(
        string nin,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        CancellationToken cancellationToken = default) =>
        _client.VerifyNinAsync(nin, firstName, lastName, dateOfBirth, cancellationToken);

    public Task<VerificationProviderResult> VerifyBiometricsAsync(
        string selfieImageBase64,
        string? referenceImageBase64 = null,
        string? idNumber = null,
        CancellationToken cancellationToken = default) =>
        _client.VerifyBiometricsAsync(selfieImageBase64, referenceImageBase64, idNumber, cancellationToken);

    public Task<VerificationProviderResult> VerifyDocumentAsync(
        DocumentType documentType,
        string documentNumber,
        string documentImageBase64,
        string? firstName = null,
        string? lastName = null,
        CancellationToken cancellationToken = default)
    {
        var idType = documentType switch
        {
            DocumentType.Nimc => "NIN_SLIP",
            DocumentType.DriversLicense => "DRIVERS_LICENSE",
            DocumentType.InternationalPassport => "PASSPORT",
            _ => "NATIONAL_ID"
        };

        return _client.VerifyDocumentAsync(documentImageBase64, idType, documentNumber, firstName, lastName, cancellationToken);
    }

    public Task<VerificationProviderResult> ScreenIndividualAsync(
        string fullName,
        DateTime? dateOfBirth = null,
        string? countryCode = "NG",
        CancellationToken cancellationToken = default) =>
        _client.ScreenAmlAsync(fullName, isEntity: false, cancellationToken);

    public Task<VerificationProviderResult> ScreenEntityAsync(
        string entityName,
        string? registrationNumber = null,
        string? countryCode = "NG",
        CancellationToken cancellationToken = default) =>
        _client.ScreenAmlAsync(entityName, isEntity: true, cancellationToken);

    public Task<VerificationProviderResult> VerifyBusinessAsync(
        string cacNumber,
        string companyName,
        CancellationToken cancellationToken = default) =>
        _client.VerifyBusinessAsync(cacNumber, companyName, cancellationToken);

    public Task<VerificationProviderResult> GetBeneficialOwnersAsync(
        string cacNumber,
        CancellationToken cancellationToken = default) =>
        _client.VerifyBusinessAsync(cacNumber, null, cancellationToken);
}
