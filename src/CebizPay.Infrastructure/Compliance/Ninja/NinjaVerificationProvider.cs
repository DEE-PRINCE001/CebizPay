#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Infrastructure.Compliance.Ninja;

/// <summary>
/// Ninja provider adapter implementing identity, business, and AML compliance capabilities.
/// </summary>
public sealed class NinjaVerificationProvider :
    IIdentityVerificationProvider,
    IAmlScreeningProvider,
    IBusinessVerificationProvider,
    IVerificationProvider
{
    private readonly INinjaClient _client;

    public VerificationProvider Provider => VerificationProvider.Ninja;

    public NinjaVerificationProvider(INinjaClient client)
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
        _client.VerifyCacAsync(cacNumber, companyName, cancellationToken);

    public Task<VerificationProviderResult> GetBeneficialOwnersAsync(
        string cacNumber,
        CancellationToken cancellationToken = default) =>
        _client.VerifyCacAsync(cacNumber, string.Empty, cancellationToken);
}
