using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Provider abstraction for Anti-Money Laundering (AML), PEP, and Sanctions watchlist screening.
/// </summary>
public interface IAmlScreeningProvider
{
    /// <summary>Provider identifier.</summary>
    VerificationProvider Provider { get; }

    /// <summary>
    /// Screens an individual against global sanctions (OFAC, UN, EU), PEP lists, and adverse media.
    /// </summary>
    Task<VerificationProviderResult> ScreenIndividualAsync(
        string fullName,
        DateTime? dateOfBirth = null,
        string? countryCode = "NG",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Screens a legal entity or organization against corporate sanctions and watchlist registries.
    /// </summary>
    Task<VerificationProviderResult> ScreenEntityAsync(
        string entityName,
        string? registrationNumber = null,
        string? countryCode = "NG",
        CancellationToken cancellationToken = default);
}
