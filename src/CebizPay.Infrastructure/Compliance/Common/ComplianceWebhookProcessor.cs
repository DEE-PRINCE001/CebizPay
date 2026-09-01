#pragma warning disable CA1848, CA1873, CA1305, CS1591
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Compliance.Events;
using CebizPay.Infrastructure.Compliance.Dojah;
using CebizPay.Infrastructure.Compliance.Ninja;
using CebizPay.Infrastructure.Compliance.SmileId;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Compliance.Common;

/// <summary>
/// Service responsible for authenticating, deduplicating, and asynchronously processing
/// inbound compliance webhook callbacks from external verification providers.
/// </summary>
public sealed class ComplianceWebhookProcessor : IComplianceWebhookProcessor
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IComplianceWebhookSignatureVerifier _signatureVerifier;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<ComplianceWebhookProcessor> _logger;
    private readonly DojahOptions _dojahOptions;
    private readonly SmileIdOptions _smileIdOptions;
    private readonly NinjaOptions _ninjaOptions;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ComplianceWebhookProcessor(
        IApplicationDbContext dbContext,
        IComplianceWebhookSignatureVerifier signatureVerifier,
        IOutboxService outboxService,
        IOptions<DojahOptions> dojahOptions,
        IOptions<SmileIdOptions> smileIdOptions,
        IOptions<NinjaOptions> ninjaOptions,
        ILogger<ComplianceWebhookProcessor> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _dojahOptions = dojahOptions?.Value ?? new DojahOptions();
        _smileIdOptions = smileIdOptions?.Value ?? new SmileIdOptions();
        _ninjaOptions = ninjaOptions?.Value ?? new NinjaOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ComplianceWebhookProcessingResult> ProcessWebhookAsync(
        VerificationProvider provider,
        string rawPayload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
            return ComplianceWebhookProcessingResult.InvalidPayload("Empty webhook payload.");

        // 1. Verify provider signature if secret is configured
        var secret = GetProviderWebhookSecret(provider);
        if (!string.IsNullOrWhiteSpace(secret))
        {
            var isValid = _signatureVerifier.VerifySignature(provider, rawPayload, headers, secret);
            if (!isValid)
            {
                _logger.LogWarning("Invalid webhook signature for provider {Provider}.", provider);
                return ComplianceWebhookProcessingResult.InvalidSignature();
            }
        }

        // 2. Compute payload SHA256 hash for deduplication and audit
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawPayload)));

        // 3. Extract event ID and metadata
        var (providerEventId, eventType, reference, resultStatus, confidenceScore, failureReason) =
            ParseWebhookPayload(provider, rawPayload);

        providerEventId ??= $"EVT-{payloadHash[..16]}";
        eventType ??= "verification.completed";

        ComplianceMetrics.RecordWebhook(provider, eventType);

        // 4. Deduplicate webhook against database
        var existingEvent = await _dbContext.ComplianceWebhookEvents
            .FirstOrDefaultAsync(e => e.Provider == provider && (e.ProviderEventId == providerEventId || e.PayloadHash == payloadHash), cancellationToken);

        if (existingEvent != null)
        {
            ComplianceMetrics.RecordWebhookDuplicate(provider);
            _logger.LogInformation("Duplicate compliance webhook received for provider {Provider}, EventId {EventId}.", provider, providerEventId);
            return ComplianceWebhookProcessingResult.Duplicate(providerEventId);
        }

        var webhookEvent = ComplianceWebhookEvent.Create(provider, providerEventId, eventType, payloadHash);
        _dbContext.ComplianceWebhookEvents.Add(webhookEvent);

        // 5. Correlate with internal VerificationOperation if reference exists
        VerificationOperation? operation = null;
        if (!string.IsNullOrWhiteSpace(reference))
        {
            operation = await _dbContext.VerificationOperations
                .Include(o => o.Evidences)
                .FirstOrDefaultAsync(o => o.Reference == reference || o.Evidences.Any(e => e.ProviderReference == reference), cancellationToken);
        }

        if (operation != null && operation.Status is VerificationStatus.Initiated or VerificationStatus.Processing or VerificationStatus.PendingCallback)
        {
            var evidence = VerificationEvidence.Create(
                verificationOperationId: operation.Id,
                verificationType: operation.VerificationType,
                capability: operation.Capability,
                provider: provider,
                resultStatus: resultStatus,
                userId: operation.UserId,
                organizationId: operation.OrganizationId,
                providerReference: reference,
                confidenceScore: confidenceScore,
                verifiedAtUtc: DateTime.UtcNow,
                failureReason: failureReason);

            operation.AddEvidence(evidence);
            _dbContext.VerificationEvidences.Add(evidence);

            if (resultStatus == VerificationResultStatus.Match)
            {
                operation.MarkCompleted();
                _outboxService.Write(new VerificationCompletedDomainEvent(
                    operation.Id, operation.Reference, operation.VerificationType, operation.Capability, provider, resultStatus, operation.UserId, operation.OrganizationId, DateTime.UtcNow));
            }
            else if (resultStatus == VerificationResultStatus.ReviewRequired)
            {
                operation.MarkReviewRequired(failureReason ?? "Flagged by provider callback.");
            }
            else
            {
                operation.MarkFailed(failureReason ?? "Verification rejected by provider callback.");
                _outboxService.Write(new VerificationFailedDomainEvent(
                    operation.Id, operation.Reference, operation.VerificationType, operation.Capability, failureReason ?? "Failed", operation.UserId, operation.OrganizationId, DateTime.UtcNow));
            }

            webhookEvent.MarkProcessed(operation.Id);
        }
        else
        {
            webhookEvent.MarkProcessed();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ComplianceWebhookProcessingResult.Processed(providerEventId, "Compliance webhook processed successfully.", operation?.Id);
    }

    private string? GetProviderWebhookSecret(VerificationProvider provider) =>
        provider switch
        {
            VerificationProvider.Dojah => _dojahOptions.WebhookSecret,
            VerificationProvider.SmileId => _smileIdOptions.WebhookSecret,
            VerificationProvider.Ninja => _ninjaOptions.WebhookSecret,
            _ => null
        };

    private static (string? EventId, string? EventType, string? Reference, VerificationResultStatus Result, decimal? Confidence, string? FailureReason)
        ParseWebhookPayload(VerificationProvider provider, string rawPayload)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;

            if (provider == VerificationProvider.Dojah)
            {
                var eventId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() :
                              root.TryGetProperty("event_id", out var evtIdProp) ? evtIdProp.GetString() : null;
                var eventType = root.TryGetProperty("event", out var evtProp) ? evtProp.GetString() : "verification";
                var refId = root.TryGetProperty("reference_id", out var refProp) ? refProp.GetString() :
                            root.TryGetProperty("reference", out var rProp) ? rProp.GetString() : null;
                var statusStr = root.TryGetProperty("status", out var stProp) ? stProp.GetString() : null;

                var isSuccess = string.Equals(statusStr, "success", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(statusStr, "valid", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(statusStr, "approved", StringComparison.OrdinalIgnoreCase);

                return (eventId, eventType, refId, isSuccess ? VerificationResultStatus.Match : VerificationResultStatus.Mismatch, 100m, null);
            }

            if (provider == VerificationProvider.SmileId)
            {
                var jobId = root.TryGetProperty("JobId", out var jProp) ? jProp.GetString() :
                            root.TryGetProperty("job_id", out var jProp2) ? jProp2.GetString() : null;
                var resultCode = root.TryGetProperty("ResultCode", out var rcProp) ? rcProp.GetString() :
                                 root.TryGetProperty("result_code", out var rcProp2) ? rcProp2.GetString() : null;
                var resultText = root.TryGetProperty("ResultText", out var rtProp) ? rtProp.GetString() :
                                 root.TryGetProperty("result_text", out var rtProp2) ? rtProp2.GetString() : null;

                string? refId = null;
                if (root.TryGetProperty("PartnerParams", out var ppProp))
                {
                    refId = ppProp.TryGetProperty("user_id", out var ppUser) ? ppUser.GetString() :
                            ppProp.TryGetProperty("job_id", out var ppJob) ? ppJob.GetString() : null;
                }
                refId ??= jobId;

                decimal? confidence = null;
                if (root.TryGetProperty("ConfidenceValue", out var cfProp))
                {
                    if (cfProp.ValueKind == JsonValueKind.Number && cfProp.TryGetDecimal(out var cfVal))
                        confidence = cfVal;
                    else if (cfProp.ValueKind == JsonValueKind.String && decimal.TryParse(cfProp.GetString(), out var cfValParsed))
                        confidence = cfValParsed;
                }
                confidence ??= 100m;

                var resultStatus = resultCode switch
                {
                    "1012" or "0810" => VerificationResultStatus.Match,
                    "1013" => VerificationResultStatus.NotFound,
                    "1014" or "0811" => VerificationResultStatus.Mismatch,
                    "1015" or "0812" => VerificationResultStatus.ReviewRequired,
                    _ => VerificationResultStatus.Mismatch
                };

                return (jobId, "job.completed", refId, resultStatus, confidence, resultText);
            }

            if (provider == VerificationProvider.Ninja)
            {
                var refId = root.TryGetProperty("reference", out var rProp) ? rProp.GetString() :
                            root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                var eventType = root.TryGetProperty("event", out var eProp) ? eProp.GetString() : "verification.result";
                var success = root.TryGetProperty("success", out var sProp) && sProp.GetBoolean();

                return (refId, eventType, refId, success ? VerificationResultStatus.Match : VerificationResultStatus.Mismatch, 100m, null);
            }
        }
        catch
        {
            // Fallback for non-JSON or unstructured payloads
        }

        return (null, null, null, VerificationResultStatus.Mismatch, null, "Unrecognized webhook format");
    }
}
