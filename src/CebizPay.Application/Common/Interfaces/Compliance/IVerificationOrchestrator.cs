using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Enums;

namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Core orchestrator for capability-based provider verification, safe technical failover,
/// immutable evidence capture, and transactional outbox event emission.
/// </summary>
public interface IVerificationOrchestrator
{
    /// <summary>
    /// Orchestrates BVN verification across primary and fallback identity providers.
    /// </summary>
    Task<VerificationOperationResponse> VerifyBvnAsync(
        string userId,
        string bvn,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Orchestrates NIN verification across primary and fallback identity providers.
    /// </summary>
    Task<VerificationOperationResponse> VerifyNinAsync(
        string userId,
        string nin,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Orchestrates biometric liveness and 1:1 facial matching across primary and fallback providers.
    /// </summary>
    Task<VerificationOperationResponse> VerifyBiometricsAsync(
        string userId,
        string selfieImageBase64,
        string? referenceImageBase64 = null,
        string? idNumber = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Orchestrates government document verification across primary and fallback providers.
    /// </summary>
    Task<VerificationOperationResponse> VerifyDocumentAsync(
        string userId,
        DocumentType documentType,
        string documentNumber,
        string documentImageBase64,
        string? firstName = null,
        string? lastName = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Orchestrates individual AML / PEP / Sanctions screening.
    /// </summary>
    Task<VerificationOperationResponse> ScreenIndividualAmlAsync(
        string userId,
        string fullName,
        DateTime? dateOfBirth = null,
        string? countryCode = "NG",
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Orchestrates organization / legal entity AML / Sanctions screening.
    /// </summary>
    Task<VerificationOperationResponse> ScreenEntityAmlAsync(
        Guid organizationId,
        string entityName,
        string? registrationNumber = null,
        string? countryCode = "NG",
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Orchestrates Corporate / CAC business registry verification.
    /// </summary>
    Task<VerificationOperationResponse> VerifyBusinessAsync(
        Guid organizationId,
        string cacNumber,
        string companyName,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Orchestrates ultimate beneficial ownership and director inquiry.
    /// </summary>
    Task<VerificationOperationResponse> GetBeneficialOwnersAsync(
        Guid organizationId,
        string cacNumber,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);
}
