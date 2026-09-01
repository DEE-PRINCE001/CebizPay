using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Domain.Compliance.Entities;

/// <summary>
/// Root domain entity tracking an end-to-end verification operation lifecycle,
/// provider orchestration, and associated immutable evidence collection.
/// </summary>
public class VerificationOperation
{
    private readonly List<VerificationEvidence> _evidences = new();

    /// <summary>Unique operation identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Canonical CebizPay internal reference (e.g. CBZKYC-..., CBZKYB-...).</summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>Scope of verification (Individual KYC vs Organization KYB).</summary>
    public VerificationType VerificationType { get; private set; }

    /// <summary>Specific compliance capability being validated.</summary>
    public VerificationCapability Capability { get; private set; }

    /// <summary>User ID for natural person verification.</summary>
    public string? UserId { get; private set; }

    /// <summary>Organization ID for corporate legal entity verification.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Current lifecycle status.</summary>
    public VerificationStatus Status { get; private set; } = VerificationStatus.Initiated;

    /// <summary>Configured primary verification provider.</summary>
    public VerificationProvider PrimaryProvider { get; private set; }

    /// <summary>Currently active provider executing or producing the latest result.</summary>
    public VerificationProvider ActiveProvider { get; private set; }

    /// <summary>Indicates whether provider failover was engaged due to technical failure.</summary>
    public bool UsedFallback { get; private set; }

    /// <summary>Optional client-supplied idempotency key.</summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>Failure reason if the overall operation failed.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Operation initiation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Operation completion timestamp.</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Collection of all immutable verification evidence gathered during this operation.</summary>
    public IReadOnlyCollection<VerificationEvidence> Evidences => _evidences.AsReadOnly();

    private VerificationOperation() { } // EF Core

    /// <summary>
    /// Generates a canonical verification reference string based on verification type.
    /// </summary>
    public static string GenerateReference(VerificationType type)
    {
        var prefix = type == VerificationType.IndividualKyc ? "CBZKYC" : "CBZKYB";
        return $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32].ToUpperInvariant();
    }

    /// <summary>
    /// Creates a new verification operation.
    /// </summary>
    public static VerificationOperation Create(
        string reference,
        VerificationType verificationType,
        VerificationCapability capability,
        VerificationProvider primaryProvider,
        string? userId = null,
        Guid? organizationId = null,
        string? idempotencyKey = null)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("Reference is required.", nameof(reference));

        if (verificationType == VerificationType.IndividualKyc && string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required for Individual KYC operation.", nameof(userId));

        if (verificationType == VerificationType.OrganizationKyb && (!organizationId.HasValue || organizationId.Value == Guid.Empty))
            throw new ArgumentException("OrganizationId is required for Organization KYB operation.", nameof(organizationId));

        return new VerificationOperation
        {
            Id = Guid.NewGuid(),
            Reference = reference.Trim(),
            VerificationType = verificationType,
            Capability = capability,
            PrimaryProvider = primaryProvider,
            ActiveProvider = primaryProvider,
            UsedFallback = false,
            UserId = userId?.Trim(),
            OrganizationId = organizationId,
            IdempotencyKey = idempotencyKey?.Trim(),
            Status = VerificationStatus.Initiated,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Transitions the operation into processing state.
    /// </summary>
    public void MarkProcessing()
    {
        if (Status == VerificationStatus.Completed || Status == VerificationStatus.Cancelled)
            throw new InvalidOperationException($"Cannot transition to Processing from terminal status {Status}.");

        Status = VerificationStatus.Processing;
    }

    /// <summary>
    /// Transitions the operation into pending callback state for asynchronous providers.
    /// </summary>
    public void MarkPendingCallback()
    {
        if (Status == VerificationStatus.Completed || Status == VerificationStatus.Cancelled)
            throw new InvalidOperationException($"Cannot transition to PendingCallback from terminal status {Status}.");

        Status = VerificationStatus.PendingCallback;
    }

    /// <summary>
    /// Records fallback engagement when primary encounters a technical failure.
    /// </summary>
    public void RecordFallback(VerificationProvider fallbackProvider)
    {
        UsedFallback = true;
        ActiveProvider = fallbackProvider;
        Status = VerificationStatus.Processing;
    }

    /// <summary>
    /// Marks the verification operation as definitively completed.
    /// </summary>
    public void MarkCompleted(DateTime? now = null)
    {
        Status = VerificationStatus.Completed;
        CompletedAtUtc = now ?? DateTime.UtcNow;
        FailureReason = null;
    }

    /// <summary>
    /// Marks the verification operation as failed.
    /// </summary>
    public void MarkFailed(string reason, DateTime? now = null)
    {
        Status = VerificationStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Verification failed." : reason.Trim();
        CompletedAtUtc = now ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the verification operation as requiring human compliance review.
    /// </summary>
    public void MarkReviewRequired(string reason, DateTime? now = null)
    {
        Status = VerificationStatus.ReviewRequired;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Manual compliance review required." : reason.Trim();
        CompletedAtUtc = now ?? DateTime.UtcNow;
    }

    /// <summary>
    /// Appends immutable verification evidence to the operation.
    /// </summary>
    public void AddEvidence(VerificationEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        _evidences.Add(evidence);
    }
}
