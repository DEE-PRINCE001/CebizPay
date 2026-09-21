#pragma warning disable CA1848, CA1873, CA1305, CS1591
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Compliance.Events;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
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
/// Synchronizes verified legal demographics and provisions dedicated Monnify virtual accounts upon KYC match.
/// </summary>
public sealed class ComplianceWebhookProcessor : IComplianceWebhookProcessor
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IComplianceWebhookSignatureVerifier _signatureVerifier;
    private readonly IOutboxService _outboxService;
    private readonly ICddService? _cddService;
    private readonly IVirtualAccountService? _virtualAccountService;
    private readonly ILogger<ComplianceWebhookProcessor> _logger;
    private readonly DojahOptions _dojahOptions;
    private readonly SmileIdOptions _smileIdOptions;
    private readonly NinjaOptions _ninjaOptions;

    public ComplianceWebhookProcessor(
        IApplicationDbContext dbContext,
        IComplianceWebhookSignatureVerifier signatureVerifier,
        IOutboxService outboxService,
        IOptions<DojahOptions> dojahOptions,
        IOptions<SmileIdOptions> smileIdOptions,
        IOptions<NinjaOptions> ninjaOptions,
        ILogger<ComplianceWebhookProcessor> logger,
        ICddService? cddService = null,
        IVirtualAccountService? virtualAccountService = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
        _dojahOptions = dojahOptions?.Value ?? new DojahOptions();
        _smileIdOptions = smileIdOptions?.Value ?? new SmileIdOptions();
        _ninjaOptions = ninjaOptions?.Value ?? new NinjaOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cddService = cddService;
        _virtualAccountService = virtualAccountService;
    }

    public Task<ComplianceWebhookProcessingResult> ProcessWebhookAsync(
        VerificationProvider provider,
        string rawPayload,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        return ProcessPayloadCoreAsync(provider, rawPayload, headers, verifySignature: true, cancellationToken);
    }

    public Task<ComplianceWebhookProcessingResult> ProcessDirectPayloadAsync(
        VerificationProvider provider,
        string rawPayload,
        CancellationToken cancellationToken = default)
    {
        return ProcessPayloadCoreAsync(provider, rawPayload, headers: null, verifySignature: false, cancellationToken);
    }

    private async Task<ComplianceWebhookProcessingResult> ProcessPayloadCoreAsync(
        VerificationProvider provider,
        string rawPayload,
        IReadOnlyDictionary<string, string>? headers,
        bool verifySignature,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
            return ComplianceWebhookProcessingResult.InvalidPayload("Empty webhook payload.");

        // 1. Verify provider signature if configured and verifySignature is enabled
        if (verifySignature && headers != null)
        {
            if (provider == VerificationProvider.Dojah)
            {
                var isValid = false;
                var checkedAny = false;

                if (!string.IsNullOrWhiteSpace(_dojahOptions.WebhookSecret))
                {
                    checkedAny = true;
                    isValid = _signatureVerifier.VerifySignature(provider, rawPayload, headers, _dojahOptions.WebhookSecret);
                }

                if (!isValid && !string.IsNullOrWhiteSpace(_dojahOptions.PrivateKey) &&
                    !string.Equals(_dojahOptions.PrivateKey, _dojahOptions.WebhookSecret, StringComparison.Ordinal))
                {
                    checkedAny = true;
                    isValid = _signatureVerifier.VerifySignature(provider, rawPayload, headers, _dojahOptions.PrivateKey);
                }

                if (checkedAny && !isValid)
                {
                    _logger.LogWarning("Invalid webhook signature for provider {Provider}.", provider);
                    return ComplianceWebhookProcessingResult.InvalidSignature();
                }
            }
            else
            {
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
            }
        }

        // 2. Compute payload SHA256 hash for deduplication and audit
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawPayload)));

        // 3. Extract event ID, metadata and rich demographic data
        var parsed = ParseWebhookPayload(provider, rawPayload);

        var providerEventId = parsed.EventId ?? $"EVT-{payloadHash[..16]}";
        var eventType = parsed.EventType ?? "verification.completed";

        ComplianceMetrics.RecordWebhook(provider, eventType);

        // 4. Deduplicate webhook against database
        var existingEvent = await _dbContext.ComplianceWebhookEvents
            .FirstOrDefaultAsync(e => e.Provider == provider && (e.ProviderEventId == providerEventId || e.PayloadHash == payloadHash), cancellationToken)
            .ConfigureAwait(false);

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
        if (!string.IsNullOrWhiteSpace(parsed.Reference))
        {
            operation = await _dbContext.VerificationOperations
                .Include(o => o.Evidences)
                .FirstOrDefaultAsync(o => o.Reference == parsed.Reference || o.Evidences.Any(e => e.ProviderReference == parsed.Reference), cancellationToken)
                .ConfigureAwait(false);
        }

        if (operation == null && !string.IsNullOrWhiteSpace(parsed.UserId))
        {
            operation = await _dbContext.VerificationOperations
                .Include(o => o.Evidences)
                .Where(o => o.UserId == parsed.UserId && o.PrimaryProvider == provider && o.Status != VerificationStatus.Completed)
                .OrderByDescending(o => o.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (operation != null && operation.Status is VerificationStatus.Initiated or VerificationStatus.Processing or VerificationStatus.PendingCallback)
        {
            var evidence = VerificationEvidence.Create(
                verificationOperationId: operation.Id,
                verificationType: operation.VerificationType,
                capability: operation.Capability,
                provider: provider,
                resultStatus: parsed.Result,
                userId: operation.UserId,
                organizationId: operation.OrganizationId,
                providerReference: parsed.Reference,
                confidenceScore: parsed.Confidence,
                verifiedAtUtc: DateTime.UtcNow,
                failureReason: parsed.FailureReason);

            operation.AddEvidence(evidence);
            _dbContext.VerificationEvidences.Add(evidence);

            if (parsed.Result == VerificationResultStatus.Match)
            {
                operation.MarkCompleted();
                _outboxService.Write(new VerificationCompletedDomainEvent(
                    operation.Id, operation.Reference, operation.VerificationType, operation.Capability, provider, parsed.Result, operation.UserId, operation.OrganizationId, DateTime.UtcNow));

                // Individual KYC Automation: Legal Demographic Sync, Promotion, CDD Tier, & Virtual Account Creation
                if (operation.VerificationType == VerificationType.IndividualKyc && !string.IsNullOrWhiteSpace(operation.UserId))
                {
                    await HandleIndividualKycMatchAsync(operation.UserId, parsed, cancellationToken).ConfigureAwait(false);
                }
            }
            else if (parsed.Result == VerificationResultStatus.ReviewRequired)
            {
                operation.MarkReviewRequired(parsed.FailureReason ?? "Flagged by provider callback.");
            }
            else
            {
                operation.MarkFailed(parsed.FailureReason ?? "Verification rejected by provider callback.");
                _outboxService.Write(new VerificationFailedDomainEvent(
                    operation.Id, operation.Reference, operation.VerificationType, operation.Capability, parsed.FailureReason ?? "Failed", operation.UserId, operation.OrganizationId, DateTime.UtcNow));
            }

            webhookEvent.MarkProcessed(operation.Id);
        }
        else
        {
            webhookEvent.MarkProcessed();
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ComplianceWebhookProcessingResult.Processed(providerEventId, "Compliance webhook processed successfully.", operation?.Id);
    }

    private async Task HandleIndividualKycMatchAsync(
        string userId,
        ParsedWebhookData parsed,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.IndividualProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        if (profile == null)
            return;

        // 1. Synchronize legal demographics with official registry records (NIBSS/NIMC)
        if (!string.IsNullOrWhiteSpace(parsed.FirstName) && !string.IsNullOrWhiteSpace(parsed.LastName))
        {
            profile.SynchronizeLegalIdentity(parsed.FirstName, parsed.LastName, parsed.MiddleName, parsed.PhotoUrl);

            _dbContext.AuditLogs.Add(AuditLog.Create(
                actorId: "DojahWebhook",
                action: AuditActions.KycVerified,
                resourceType: AuditResourceTypes.User,
                resourceId: profile.UserId,
                afterJson: JsonSerializer.Serialize(new
                {
                    FirstName = profile.FirstName,
                    LastName = profile.LastName,
                    MiddleName = profile.MiddleName,
                    Source = "Official Registry via Dojah Webhook"
                })));
        }

        // 2. Promote KYC status to Verified if not already verified
        var oldKycStatus = profile.KycStatus;
        if (profile.KycStatus != KycStatus.Verified)
        {
            profile.SetKycStatus(KycStatus.Verified);
            _outboxService.Write(new KycStatusChangedDomainEvent(
                profile.UserId,
                oldKycStatus,
                KycStatus.Verified,
                "Verified via automated provider identity match.",
                DateTime.UtcNow));
        }

        // 3. Auto-approve pending KYC documents for this user
        var pendingDocs = await _dbContext.KycDocuments
            .Where(d => d.UserId == profile.UserId && d.Status == KycStatus.Pending)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var doc in pendingDocs)
        {
            doc.Approve("DojahAutomatedSystem", DateTime.UtcNow);
        }

        // 4. Record verified BVN document if available and not yet recorded
        if (!string.IsNullOrWhiteSpace(parsed.VerifiedBvn))
        {
            var hasBvnDoc = await _dbContext.KycDocuments
                .AnyAsync(d => d.UserId == profile.UserId && d.DocumentNumber == parsed.VerifiedBvn, cancellationToken)
                .ConfigureAwait(false);

            if (!hasBvnDoc)
            {
                var bvnDoc = new KycDocument(profile.UserId, DocumentType.Nimc, parsed.VerifiedBvn, "dojah://verified-bvn");
                bvnDoc.Approve("DojahAutomatedSystem", DateTime.UtcNow);
                _dbContext.KycDocuments.Add(bvnDoc);
            }
        }

        // 5. Re-evaluate CDD and statutory CBN KYC Tier (Tier 1/2/3)
        if (_cddService != null)
        {
            try
            {
                await _cddService.EvaluateCddAsync(RiskSubjectType.Individual, profile.UserId, null, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating CDD profile for user {UserId} following KYC verification match.", profile.UserId);
            }
        }

        // 6. Auto-provision dedicated Monnify NUBAN virtual account with verified legal details and BVN
        if (_virtualAccountService != null)
        {
            try
            {
                await _virtualAccountService.ProvisionIndividualVirtualAccountAsync(
                    profile.UserId,
                    Currency.NGN,
                    PaymentProvider.Monnify,
                    parsed.VerifiedBvn,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error automatically provisioning Monnify virtual account for user {UserId} after KYC verification.", profile.UserId);
            }
        }
    }

    private string? GetProviderWebhookSecret(VerificationProvider provider) =>
        provider switch
        {
            VerificationProvider.Dojah => _dojahOptions.WebhookSecret,
            VerificationProvider.SmileId => _smileIdOptions.WebhookSecret,
            VerificationProvider.Ninja => _ninjaOptions.WebhookSecret,
            _ => null
        };

    private sealed record ParsedWebhookData(
        string? EventId,
        string? EventType,
        string? Reference,
        VerificationResultStatus Result,
        decimal? Confidence,
        string? FailureReason,
        string? VerifiedBvn = null,
        string? VerifiedNin = null,
        string? FirstName = null,
        string? LastName = null,
        string? MiddleName = null,
        string? PhotoUrl = null,
        string? UserId = null);

    private static ParsedWebhookData ParseWebhookPayload(VerificationProvider provider, string rawPayload)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;

            if (provider == VerificationProvider.Dojah)
            {
                var dataElement = root.TryGetProperty("data", out var dProp) && dProp.ValueKind == JsonValueKind.Object
                    ? dProp
                    : root;

                var eventId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() :
                              root.TryGetProperty("event_id", out var evtIdProp) ? evtIdProp.GetString() :
                              dataElement.TryGetProperty("id", out var dIdProp) ? dIdProp.GetString() : null;

                var eventType = root.TryGetProperty("event", out var evtProp) ? evtProp.GetString() : "verification";

                // Extract metadata (userId and internal reference)
                string? metadataUserId = null;
                string? refId = null;
                if (dataElement.TryGetProperty("metadata", out var metaProp) || root.TryGetProperty("metadata", out metaProp))
                {
                    if (metaProp.TryGetProperty("user_id", out var mUserProp))
                        metadataUserId = mUserProp.GetString();

                    if (metaProp.TryGetProperty("reference_id", out var mRefIdProp))
                        refId = mRefIdProp.GetString();
                    else if (metaProp.TryGetProperty("reference", out var mRefProp))
                        refId = mRefProp.GetString();
                }

                if (string.IsNullOrWhiteSpace(refId))
                {
                    refId = root.TryGetProperty("reference_id", out var refProp) ? refProp.GetString() :
                            root.TryGetProperty("reference", out var rProp) ? rProp.GetString() :
                            dataElement.TryGetProperty("reference_id", out var dRefProp) ? dRefProp.GetString() :
                            dataElement.TryGetProperty("reference", out var dRProp) ? dRProp.GetString() : null;
                }

                // Determine success status (supporting boolean or status strings)
                var isSuccess = false;
                if (root.TryGetProperty("status", out var rStProp))
                {
                    if (rStProp.ValueKind == JsonValueKind.True)
                        isSuccess = true;
                    else if (rStProp.ValueKind == JsonValueKind.String)
                    {
                        var s = rStProp.GetString();
                        isSuccess = string.Equals(s, "success", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(s, "valid", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(s, "approved", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(s, "completed", StringComparison.OrdinalIgnoreCase);
                    }
                }

                if (!isSuccess && dataElement.TryGetProperty("status", out var dStProp))
                {
                    if (dStProp.ValueKind == JsonValueKind.True)
                        isSuccess = true;
                    else if (dStProp.ValueKind == JsonValueKind.String)
                    {
                        var s = dStProp.GetString();
                        isSuccess = string.Equals(s, "success", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(s, "valid", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(s, "approved", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(s, "completed", StringComparison.OrdinalIgnoreCase);
                    }
                }

                if (!isSuccess && root.TryGetProperty("verification_status", out var vsProp) && vsProp.ValueKind == JsonValueKind.String)
                {
                    isSuccess = string.Equals(vsProp.GetString(), "Completed", StringComparison.OrdinalIgnoreCase);
                }

                // Extract BVN & NIN
                string? bvn = null;
                string? nin = null;
                if (dataElement.TryGetProperty("bvn", out var bvnProp) && bvnProp.ValueKind == JsonValueKind.String)
                    bvn = bvnProp.GetString();
                if (dataElement.TryGetProperty("nin", out var ninProp) && ninProp.ValueKind == JsonValueKind.String)
                    nin = ninProp.GetString();

                // Extract legal demographics
                string? firstName = null;
                string? lastName = null;
                string? middleName = null;
                string? photoUrl = null;

                // Support Dojah widget official structure: data.government_data.data.bvn/nin.entity
                if (dataElement.TryGetProperty("government_data", out var govProp) && govProp.ValueKind == JsonValueKind.Object &&
                    govProp.TryGetProperty("data", out var govDataProp) && govDataProp.ValueKind == JsonValueKind.Object)
                {
                    if (govDataProp.TryGetProperty("bvn", out var gBvnProp) && gBvnProp.ValueKind == JsonValueKind.Object &&
                        gBvnProp.TryGetProperty("entity", out var bvnEntity) && bvnEntity.ValueKind == JsonValueKind.Object)
                    {
                        if (string.IsNullOrWhiteSpace(bvn) && bvnEntity.TryGetProperty("bvn", out var bVal)) bvn = bVal.GetString();
                        if (string.IsNullOrWhiteSpace(firstName) && bvnEntity.TryGetProperty("first_name", out var fVal)) firstName = fVal.GetString();
                        if (string.IsNullOrWhiteSpace(lastName) && bvnEntity.TryGetProperty("last_name", out var lVal)) lastName = lVal.GetString();
                        if (string.IsNullOrWhiteSpace(middleName) && bvnEntity.TryGetProperty("middle_name", out var mVal)) middleName = mVal.GetString();
                    }

                    if (govDataProp.TryGetProperty("nin", out var gNinProp) && gNinProp.ValueKind == JsonValueKind.Object &&
                        gNinProp.TryGetProperty("entity", out var ninEntity) && ninEntity.ValueKind == JsonValueKind.Object)
                    {
                        if (string.IsNullOrWhiteSpace(nin) && ninEntity.TryGetProperty("nin", out var nVal)) nin = nVal.GetString();
                        if (string.IsNullOrWhiteSpace(firstName) && ninEntity.TryGetProperty("first_name", out var fnVal)) firstName = fnVal.GetString();
                        if (string.IsNullOrWhiteSpace(lastName) && ninEntity.TryGetProperty("last_name", out var lnVal)) lastName = lnVal.GetString();
                        if (string.IsNullOrWhiteSpace(middleName) && ninEntity.TryGetProperty("middle_name", out var mnVal)) middleName = mnVal.GetString();
                    }
                }

                // Support direct id data: data.id.data.id_data
                if (dataElement.TryGetProperty("id", out var idDataOuter) && idDataOuter.ValueKind == JsonValueKind.Object &&
                    idDataOuter.TryGetProperty("data", out var idDataInner) && idDataInner.ValueKind == JsonValueKind.Object &&
                    idDataInner.TryGetProperty("id_data", out var idEntity) && idEntity.ValueKind == JsonValueKind.Object)
                {
                    if (string.IsNullOrWhiteSpace(firstName) && idEntity.TryGetProperty("first_name", out var ifnVal)) firstName = ifnVal.GetString();
                    if (string.IsNullOrWhiteSpace(lastName) && idEntity.TryGetProperty("last_name", out var ilnVal)) lastName = ilnVal.GetString();
                }

                // Support verification wrapper
                if (dataElement.TryGetProperty("verification", out var vProp) && vProp.ValueKind == JsonValueKind.Object)
                {
                    if (string.IsNullOrWhiteSpace(bvn) && vProp.TryGetProperty("bvn", out var vbProp) && vbProp.ValueKind == JsonValueKind.Object)
                    {
                        if (vbProp.TryGetProperty("value", out var vbVal))
                            bvn = vbVal.GetString();
                    }
                    if (string.IsNullOrWhiteSpace(nin) && vProp.TryGetProperty("nin", out var vnProp) && vnProp.ValueKind == JsonValueKind.Object)
                    {
                        if (vnProp.TryGetProperty("value", out var vnVal))
                            nin = vnVal.GetString();
                    }
                }

                // Support user_data object
                if (dataElement.TryGetProperty("user_data", out var uProp) && uProp.ValueKind == JsonValueKind.Object)
                {
                    if (string.IsNullOrWhiteSpace(firstName) && uProp.TryGetProperty("first_name", out var fnProp)) firstName = fnProp.GetString();
                    if (string.IsNullOrWhiteSpace(lastName) && uProp.TryGetProperty("last_name", out var lnProp)) lastName = lnProp.GetString();
                    if (string.IsNullOrWhiteSpace(middleName) && uProp.TryGetProperty("middle_name", out var mnProp)) middleName = mnProp.GetString();
                    if (string.IsNullOrWhiteSpace(photoUrl) && uProp.TryGetProperty("photo", out var phProp)) photoUrl = phProp.GetString();
                }

                if (string.IsNullOrWhiteSpace(firstName) && dataElement.TryGetProperty("first_name", out var dfnProp))
                    firstName = dfnProp.GetString();
                if (string.IsNullOrWhiteSpace(lastName) && dataElement.TryGetProperty("last_name", out var dlnProp))
                    lastName = dlnProp.GetString();
                if (string.IsNullOrWhiteSpace(middleName) && dataElement.TryGetProperty("middle_name", out var dmnProp))
                    middleName = dmnProp.GetString();

                // Extract selfie URL
                if (string.IsNullOrWhiteSpace(photoUrl))
                {
                    if (dataElement.TryGetProperty("selfie", out var selfieProp) && selfieProp.ValueKind == JsonValueKind.Object &&
                        selfieProp.TryGetProperty("data", out var sData) && sData.ValueKind == JsonValueKind.Object &&
                        sData.TryGetProperty("selfie_url", out var suProp))
                    {
                        photoUrl = suProp.GetString();
                    }
                    else if (root.TryGetProperty("selfie_url", out var rSuProp))
                    {
                        photoUrl = rSuProp.GetString();
                    }
                }

                return new ParsedWebhookData(
                    EventId: eventId,
                    EventType: eventType,
                    Reference: refId,
                    Result: isSuccess ? VerificationResultStatus.Match : VerificationResultStatus.Mismatch,
                    Confidence: 100m,
                    FailureReason: isSuccess ? null : "Verification reported failure status by Dojah.",
                    VerifiedBvn: bvn,
                    VerifiedNin: nin,
                    FirstName: firstName,
                    LastName: lastName,
                    MiddleName: middleName,
                    PhotoUrl: photoUrl,
                    UserId: metadataUserId);
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
                string? userId = null;
                if (root.TryGetProperty("PartnerParams", out var ppProp))
                {
                    userId = ppProp.TryGetProperty("user_id", out var ppUser) ? ppUser.GetString() : null;
                    refId = ppProp.TryGetProperty("job_id", out var ppJob) ? ppJob.GetString() : userId;
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

                return new ParsedWebhookData(
                    EventId: jobId,
                    EventType: "job.completed",
                    Reference: refId,
                    Result: resultStatus,
                    Confidence: confidence,
                    FailureReason: resultText,
                    UserId: userId);
            }

            if (provider == VerificationProvider.Ninja)
            {
                var refId = root.TryGetProperty("reference", out var rProp) ? rProp.GetString() :
                            root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                var eventType = root.TryGetProperty("event", out var eProp) ? eProp.GetString() : "verification.result";
                var success = root.TryGetProperty("success", out var sProp) && sProp.GetBoolean();

                return new ParsedWebhookData(
                    EventId: refId,
                    EventType: eventType,
                    Reference: refId,
                    Result: success ? VerificationResultStatus.Match : VerificationResultStatus.Mismatch,
                    Confidence: 100m,
                    FailureReason: success ? null : "Verification failed via Ninja.");
            }
        }
        catch
        {
            // Fallback for non-JSON or unstructured payloads
        }

        return new ParsedWebhookData(null, null, null, VerificationResultStatus.Mismatch, null, "Unrecognized webhook format");
    }
}
