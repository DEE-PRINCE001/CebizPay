#pragma warning disable CS1591
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;

namespace CebizPay.Domain.Payments.Events;

/// <summary>
/// Domain event published when an in-flight or ambiguous external transaction enters reconciliation.
/// </summary>
public sealed record ReconciliationStartedDomainEvent(
    Guid ReconciliationId,
    ReconciliationType ReconciliationType,
    string SourceReference,
    string Provider,
    string? ProviderReference,
    decimal? ExpectedAmount,
    Currency? Currency,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when reconciliation definitively proves external success and settles internal state.
/// </summary>
public sealed record ReconciliationResolvedSuccessDomainEvent(
    Guid ReconciliationId,
    ReconciliationType ReconciliationType,
    string SourceReference,
    string Provider,
    string? ProviderReference,
    decimal? ReconciledAmount,
    Currency? Currency,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when reconciliation definitively proves external failure without side effects.
/// </summary>
public sealed record ReconciliationResolvedFailureDomainEvent(
    Guid ReconciliationId,
    ReconciliationType ReconciliationType,
    string SourceReference,
    string Provider,
    string FailureReason,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a reconciliation discrepancy is escalated for manual administrative review.
/// </summary>
public sealed record ReconciliationEscalatedToManualReviewDomainEvent(
    Guid ReconciliationId,
    ReconciliationType ReconciliationType,
    string SourceReference,
    string Provider,
    string DiscrepancyReason,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when an outstanding recovery is created due to insufficient wallet balance on refund/chargeback.
/// </summary>
public sealed record RecoveryOutstandingCreatedDomainEvent(
    Guid RecoveryRecordId,
    Guid WalletId,
    string SourceTransactionType,
    string SourceReference,
    PaymentProvider Provider,
    decimal AmountOwed,
    Currency Currency,
    string Reason,
    DateTime OccurredOnUtc);
