#pragma warning disable CA1848, CA1873, CA1305, CS1591
using System.Diagnostics;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Compliance.Events;
using CebizPay.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Compliance.Common;

/// <summary>
/// Orchestrates capability-based compliance verification execution across primary and fallback providers,
/// captures immutable evidence records, and publishes outbox lifecycle domain events.
/// </summary>
public sealed class VerificationOrchestrator : IVerificationOrchestrator
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IVerificationRoutingService _routingService;
    private readonly IVerificationProviderFactory _providerFactory;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<VerificationOrchestrator> _logger;

    public VerificationOrchestrator(
        IApplicationDbContext dbContext,
        IVerificationRoutingService routingService,
        IVerificationProviderFactory providerFactory,
        IOutboxService outboxService,
        ILogger<VerificationOrchestrator> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _routingService = routingService ?? throw new ArgumentNullException(nameof(routingService));
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<VerificationOperationResponse> VerifyBvnAsync(
        string userId,
        string bvn,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteVerificationAsync(
            VerificationType.IndividualKyc,
            VerificationCapability.Identity,
            userId: userId,
            organizationId: null,
            idempotencyKey: idempotencyKey,
            executeProviderFunc: (provider, ct) =>
            {
                var p = _providerFactory.GetIdentityVerificationProvider(provider);
                return p.VerifyBvnAsync(bvn, firstName, lastName, dateOfBirth, ct);
            },
            cancellationToken: cancellationToken);
    }

    public async Task<VerificationOperationResponse> VerifyNinAsync(
        string userId,
        string nin,
        string firstName,
        string lastName,
        DateTime? dateOfBirth = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteVerificationAsync(
            VerificationType.IndividualKyc,
            VerificationCapability.Identity,
            userId: userId,
            organizationId: null,
            idempotencyKey: idempotencyKey,
            executeProviderFunc: (provider, ct) =>
            {
                var p = _providerFactory.GetIdentityVerificationProvider(provider);
                return p.VerifyNinAsync(nin, firstName, lastName, dateOfBirth, ct);
            },
            cancellationToken: cancellationToken);
    }

    public async Task<VerificationOperationResponse> VerifyBiometricsAsync(
        string userId,
        string selfieImageBase64,
        string? referenceImageBase64 = null,
        string? idNumber = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteVerificationAsync(
            VerificationType.IndividualKyc,
            VerificationCapability.Biometrics,
            userId: userId,
            organizationId: null,
            idempotencyKey: idempotencyKey,
            executeProviderFunc: (provider, ct) =>
            {
                var p = _providerFactory.GetBiometricVerificationProvider(provider);
                return p.VerifyBiometricsAsync(selfieImageBase64, referenceImageBase64, idNumber, ct);
            },
            cancellationToken: cancellationToken);
    }

    public async Task<VerificationOperationResponse> VerifyDocumentAsync(
        string userId,
        DocumentType documentType,
        string documentNumber,
        string documentImageBase64,
        string? firstName = null,
        string? lastName = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteVerificationAsync(
            VerificationType.IndividualKyc,
            VerificationCapability.Document,
            userId: userId,
            organizationId: null,
            idempotencyKey: idempotencyKey,
            executeProviderFunc: (provider, ct) =>
            {
                var p = _providerFactory.GetDocumentVerificationProvider(provider);
                return p.VerifyDocumentAsync(documentType, documentNumber, documentImageBase64, firstName, lastName, ct);
            },
            cancellationToken: cancellationToken);
    }

    public async Task<VerificationOperationResponse> ScreenIndividualAmlAsync(
        string userId,
        string fullName,
        DateTime? dateOfBirth = null,
        string? countryCode = "NG",
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteVerificationAsync(
            VerificationType.IndividualKyc,
            VerificationCapability.AmlScreening,
            userId: userId,
            organizationId: null,
            idempotencyKey: idempotencyKey,
            executeProviderFunc: (provider, ct) =>
            {
                var p = _providerFactory.GetAmlScreeningProvider(provider);
                return p.ScreenIndividualAsync(fullName, dateOfBirth, countryCode, ct);
            },
            cancellationToken: cancellationToken);
    }

    public async Task<VerificationOperationResponse> ScreenEntityAmlAsync(
        Guid organizationId,
        string entityName,
        string? registrationNumber = null,
        string? countryCode = "NG",
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteVerificationAsync(
            VerificationType.OrganizationKyb,
            VerificationCapability.AmlScreening,
            userId: null,
            organizationId: organizationId,
            idempotencyKey: idempotencyKey,
            executeProviderFunc: (provider, ct) =>
            {
                var p = _providerFactory.GetAmlScreeningProvider(provider);
                return p.ScreenEntityAsync(entityName, registrationNumber, countryCode, ct);
            },
            cancellationToken: cancellationToken);
    }

    public async Task<VerificationOperationResponse> VerifyBusinessAsync(
        Guid organizationId,
        string cacNumber,
        string companyName,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteVerificationAsync(
            VerificationType.OrganizationKyb,
            VerificationCapability.Business,
            userId: null,
            organizationId: organizationId,
            idempotencyKey: idempotencyKey,
            executeProviderFunc: (provider, ct) =>
            {
                var p = _providerFactory.GetBusinessVerificationProvider(provider);
                return p.VerifyBusinessAsync(cacNumber, companyName, ct);
            },
            cancellationToken: cancellationToken);
    }

    public async Task<VerificationOperationResponse> GetBeneficialOwnersAsync(
        Guid organizationId,
        string cacNumber,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteVerificationAsync(
            VerificationType.OrganizationKyb,
            VerificationCapability.BeneficialOwnership,
            userId: null,
            organizationId: organizationId,
            idempotencyKey: idempotencyKey,
            executeProviderFunc: (provider, ct) =>
            {
                var p = _providerFactory.GetBusinessVerificationProvider(provider);
                return p.GetBeneficialOwnersAsync(cacNumber, ct);
            },
            cancellationToken: cancellationToken);
    }

    private async Task<VerificationOperationResponse> ExecuteVerificationAsync(
        VerificationType verificationType,
        VerificationCapability capability,
        string? userId,
        Guid? organizationId,
        string? idempotencyKey,
        Func<VerificationProvider, CancellationToken, Task<VerificationProviderResult>> executeProviderFunc,
        CancellationToken cancellationToken)
    {
        // 1. Check idempotency if key supplied
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingOp = await _dbContext.VerificationOperations
                .Include(o => o.Evidences)
                .FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey.Trim(), cancellationToken);

            if (existingOp != null)
            {
                _logger.LogInformation("Returning idempotent verification operation {Reference}.", existingOp.Reference);
                return MapToResponse(existingOp);
            }
        }

        // 2. Resolve primary provider
        var primaryProvider = _routingService.ResolvePrimaryProvider(capability);
        var prefix = verificationType == VerificationType.IndividualKyc ? "CBZKYC" : "CBZKYB";
        var reference = $"{prefix}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();

        var operation = VerificationOperation.Create(
            reference,
            verificationType,
            capability,
            primaryProvider,
            userId: userId,
            organizationId: organizationId,
            idempotencyKey: idempotencyKey);

        operation.MarkProcessing();
        _dbContext.VerificationOperations.Add(operation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _outboxService.Write(new VerificationInitiatedDomainEvent(
            operation.Id, operation.Reference, verificationType, capability, primaryProvider, userId, organizationId, DateTime.UtcNow));

        var currentProvider = primaryProvider;
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            ComplianceMetrics.RecordRequest(capability, currentProvider);
            stopwatch.Restart();

            VerificationProviderResult result;
            try
            {
                result = await executeProviderFunc(currentProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception calling verification provider {Provider} for capability {Capability}.", currentProvider, capability);
                result = VerificationProviderResult.TechnicalFailure("PROVIDER_EXCEPTION", ex.Message);
            }

            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;

            var evidence = VerificationEvidence.Create(
                verificationOperationId: operation.Id,
                verificationType: verificationType,
                capability: capability,
                provider: currentProvider,
                resultStatus: result.ResultStatus,
                userId: userId,
                organizationId: organizationId,
                providerReference: result.ProviderReference,
                confidenceScore: result.ConfidenceScore,
                verifiedAtUtc: DateTime.UtcNow,
                safeMetadata: result.SafeMetadata,
                failureCode: result.FailureCode,
                failureReason: result.FailureReason,
                rawPayloadRef: result.RawPayloadRef);

            operation.AddEvidence(evidence);
            _dbContext.VerificationEvidences.Add(evidence);

            // A. Success / Match -> STOP
            if (result.Succeeded)
            {
                operation.MarkCompleted();
                ComplianceMetrics.RecordSuccess(capability, currentProvider, durationMs);

                _outboxService.Write(new VerificationCompletedDomainEvent(
                    operation.Id, operation.Reference, verificationType, capability, currentProvider, result.ResultStatus, userId, organizationId, DateTime.UtcNow));

                if (capability == VerificationCapability.AmlScreening)
                {
                    _outboxService.Write(new AmlScreeningCompletedDomainEvent(
                        operation.Id, operation.Reference, currentProvider, result.ResultStatus, userId, organizationId, DateTime.UtcNow));
                }
                else if (capability == VerificationCapability.Business && organizationId.HasValue)
                {
                    _outboxService.Write(new BusinessVerificationCompletedDomainEvent(
                        operation.Id, operation.Reference, currentProvider, result.ResultStatus, organizationId.Value, DateTime.UtcNow));
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                return MapToResponse(operation);
            }

            // B. Pending (Async flow) -> STOP and wait for webhook callback
            if (result.IsPending)
            {
                operation.MarkPendingCallback();
                ComplianceMetrics.RecordPending(capability, currentProvider);

                _outboxService.Write(new VerificationPendingCallbackDomainEvent(
                    operation.Id, operation.Reference, verificationType, capability, currentProvider, result.ProviderReference, userId, organizationId, DateTime.UtcNow));

                await _dbContext.SaveChangesAsync(cancellationToken);
                return MapToResponse(operation);
            }

            // C. Review Required -> STOP
            if (result.ResultStatus == VerificationResultStatus.ReviewRequired)
            {
                operation.MarkReviewRequired(result.FailureReason ?? "Review required by compliance.");
                ComplianceMetrics.RecordFailure(capability, currentProvider, "review_required", durationMs);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return MapToResponse(operation);
            }

            // D. Business Failure (Mismatch / NotFound / InvalidRequest) -> Normally STOP (No fallback)
            if (!result.IsTechnicalFailure)
            {
                operation.MarkFailed(result.FailureReason ?? "Verification failed.");
                ComplianceMetrics.RecordFailure(capability, currentProvider, "mismatch", durationMs);

                _outboxService.Write(new VerificationFailedDomainEvent(
                    operation.Id, operation.Reference, verificationType, capability, result.FailureReason ?? "Mismatch", userId, organizationId, DateTime.UtcNow));

                await _dbContext.SaveChangesAsync(cancellationToken);
                return MapToResponse(operation);
            }

            // E. Technical Failure -> Check for Fallback
            ComplianceMetrics.RecordFailure(capability, currentProvider, "technical_failure", durationMs);
            var nextFallback = _routingService.GetNextFallbackProvider(capability, currentProvider);

            if (nextFallback.HasValue)
            {
                _logger.LogWarning("Technical failure on provider {Provider}. Failing over to {FallbackProvider} for capability {Capability}.",
                    currentProvider, nextFallback.Value, capability);

                operation.RecordFallback(nextFallback.Value);
                ComplianceMetrics.RecordFallback(capability, currentProvider, nextFallback.Value);

                _outboxService.Write(new VerificationFallbackUsedDomainEvent(
                    operation.Id, operation.Reference, capability, primaryProvider, nextFallback.Value, userId, organizationId, DateTime.UtcNow));

                currentProvider = nextFallback.Value;
                // Loop to execute next fallback provider
                continue;
            }

            // All providers exhausted on technical failure
            operation.MarkFailed("Technical failure occurred across all attempted verification providers.");
            _outboxService.Write(new VerificationFailedDomainEvent(
                operation.Id, operation.Reference, verificationType, capability, "Technical failure across all providers.", userId, organizationId, DateTime.UtcNow));

            await _dbContext.SaveChangesAsync(cancellationToken);
            return MapToResponse(operation);
        }
    }

    private static VerificationOperationResponse MapToResponse(VerificationOperation op)
    {
        var latestEvidence = op.Evidences.OrderByDescending(e => e.VerifiedAtUtc).FirstOrDefault();

        return new VerificationOperationResponse(
            op.Id,
            op.Reference,
            op.VerificationType,
            op.Capability,
            op.Status,
            op.PrimaryProvider,
            op.ActiveProvider,
            op.UsedFallback,
            latestEvidence?.ResultStatus,
            latestEvidence?.ConfidenceScore,
            latestEvidence?.SafeMetadata != null ? "Evidence captured." : null,
            op.FailureReason,
            op.CreatedAtUtc,
            op.CompletedAtUtc,
            op.Evidences.Select(e => new VerificationEvidenceSummaryDto(
                e.Id,
                e.Capability,
                e.Provider,
                e.ResultStatus,
                e.ConfidenceScore,
                e.VerifiedAtUtc,
                e.ExpiresAtUtc,
                e.FailureCode,
                e.FailureReason,
                e.SafeMetadata)).ToList());
    }
}
