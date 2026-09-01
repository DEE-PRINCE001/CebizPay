#pragma warning disable CS1591
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Application.Common.Interfaces.Payments;

/// <summary>
/// Operational decisions permitted during administrative manual review of ambiguous reconciliation records.
/// </summary>
public enum ManualReviewDecision
{
    /// <summary>Confirms verified external execution and settles internal state.</summary>
    ConfirmSuccess = 1,

    /// <summary>Confirms verified external failure/non-execution and safely unlocks or fails internal state.</summary>
    ConfirmFailure = 2,

    /// <summary>Confirms external reversal/refund and records recovery if required.</summary>
    ConfirmReversal = 3,

    /// <summary>Dismisses anomaly as non-actionable or already resolved.</summary>
    Dismiss = 4
}

/// <summary>
/// High-level outcome category of a reconciliation operation.
/// </summary>
public enum ReconciliationOutcome
{
    Success,
    Failure,
    Reversed,
    Unresolved,
    ManualReviewRequired,
    Error
}

/// <summary>
/// Detailed result of a reconciliation operation.
/// </summary>
public sealed record UnifiedReconciliationResult(
    string SourceReference,
    string Provider,
    ReconciliationStatus Status,
    ReconciliationOutcome Outcome,
    string Message,
    decimal? ReconciledAmount = null,
    string? ProviderReference = null,
    string? SafeMetadata = null)
{
    public static UnifiedReconciliationResult Succeeded(
        string sourceReference,
        string provider,
        string? providerReference = null,
        decimal? reconciledAmount = null,
        string? safeMetadata = null,
        string message = "Reconciliation resolved as SUCCEEDED.") =>
        new(sourceReference, provider, ReconciliationStatus.ResolvedSuccess, ReconciliationOutcome.Success, message, reconciledAmount, providerReference, safeMetadata);

    public static UnifiedReconciliationResult Failed(
        string sourceReference,
        string provider,
        string failureReason,
        string? safeMetadata = null) =>
        new(sourceReference, provider, ReconciliationStatus.ResolvedFailure, ReconciliationOutcome.Failure, failureReason, null, null, safeMetadata);

    public static UnifiedReconciliationResult Reversed(
        string sourceReference,
        string provider,
        string reason,
        string? safeMetadata = null) =>
        new(sourceReference, provider, ReconciliationStatus.ResolvedReversed, ReconciliationOutcome.Reversed, reason, null, null, safeMetadata);

    public static UnifiedReconciliationResult StillUnresolved(
        string sourceReference,
        string provider,
        string reason) =>
        new(sourceReference, provider, ReconciliationStatus.Unresolved, ReconciliationOutcome.Unresolved, reason);

    public static UnifiedReconciliationResult RequiresManualReview(
        string sourceReference,
        string provider,
        string reason,
        string? safeMetadata = null) =>
        new(sourceReference, provider, ReconciliationStatus.ManualReview, ReconciliationOutcome.ManualReviewRequired, reason, null, null, safeMetadata);

    public static UnifiedReconciliationResult ErrorResult(
        string sourceReference,
        string provider,
        string errorMessage) =>
        new(sourceReference, provider, ReconciliationStatus.FailedPermanently, ReconciliationOutcome.Error, errorMessage);
}

/// <summary>
/// Unified reconciliation engine contract across payment attempts, bank transfers,
/// inbound virtual accounts, card funding, card refunds, and KYC/KYB verifications.
/// </summary>
public interface IReconciliationEngine
{
    /// <summary>Reconciles a specific PaymentAttempt.</summary>
    Task<UnifiedReconciliationResult> ReconcilePaymentAttemptAsync(Guid paymentAttemptId, CancellationToken cancellationToken = default);

    /// <summary>Reconciles a BankTransfer aggregate.</summary>
    Task<UnifiedReconciliationResult> ReconcileBankTransferAsync(Guid bankTransferId, CancellationToken cancellationToken = default);

    /// <summary>Reconciles an inbound FundingTransaction.</summary>
    Task<UnifiedReconciliationResult> ReconcileFundingTransactionAsync(Guid fundingTransactionId, CancellationToken cancellationToken = default);

    /// <summary>Reconciles a CardRefund.</summary>
    Task<UnifiedReconciliationResult> ReconcileCardRefundAsync(Guid cardRefundId, CancellationToken cancellationToken = default);

    /// <summary>Reconciles a Compliance VerificationOperation.</summary>
    Task<UnifiedReconciliationResult> ReconcileComplianceOperationAsync(Guid verificationOperationId, CancellationToken cancellationToken = default);

    /// <summary>Requeries provider status by arbitrary internal or provider reference.</summary>
    Task<UnifiedReconciliationResult> RequeryByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Applies an administrative manual review decision to an unresolved reconciliation record.</summary>
    Task<UnifiedReconciliationResult> ResolveManualReviewAsync(
        Guid reconciliationRecordId,
        ManualReviewDecision decision,
        string reviewerNotes,
        string reviewerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Scans and reconciles a batch of pending reconciliation records across all rails.</summary>
    Task<int> ReconcilePendingBatchAsync(int batchSize = 50, CancellationToken cancellationToken = default);
}
