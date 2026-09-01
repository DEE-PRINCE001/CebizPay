using CebizPay.Domain.Compliance.Enums;

namespace CebizPay.Domain.Compliance.Events;

/// <summary>
/// Domain event emitted when a verification operation is initiated.
/// </summary>
public sealed record VerificationInitiatedDomainEvent(
    Guid OperationId,
    string Reference,
    VerificationType VerificationType,
    VerificationCapability Capability,
    VerificationProvider PrimaryProvider,
    string? UserId,
    Guid? OrganizationId,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event emitted when a verification operation completes with definitive evidence.
/// </summary>
public sealed record VerificationCompletedDomainEvent(
    Guid OperationId,
    string Reference,
    VerificationType VerificationType,
    VerificationCapability Capability,
    VerificationProvider Provider,
    VerificationResultStatus ResultStatus,
    string? UserId,
    Guid? OrganizationId,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event emitted when a verification operation fails.
/// </summary>
public sealed record VerificationFailedDomainEvent(
    Guid OperationId,
    string Reference,
    VerificationType VerificationType,
    VerificationCapability Capability,
    string Reason,
    string? UserId,
    Guid? OrganizationId,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event emitted when an asynchronous verification operation is awaiting provider callback.
/// </summary>
public sealed record VerificationPendingCallbackDomainEvent(
    Guid OperationId,
    string Reference,
    VerificationType VerificationType,
    VerificationCapability Capability,
    VerificationProvider Provider,
    string? ProviderReference,
    string? UserId,
    Guid? OrganizationId,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event emitted when a technical failure triggers fallback to the next provider.
/// </summary>
public sealed record VerificationFallbackUsedDomainEvent(
    Guid OperationId,
    string Reference,
    VerificationCapability Capability,
    VerificationProvider PrimaryProvider,
    VerificationProvider FallbackProvider,
    string? UserId,
    Guid? OrganizationId,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event emitted when AML / PEP / Sanctions screening completes.
/// </summary>
public sealed record AmlScreeningCompletedDomainEvent(
    Guid OperationId,
    string Reference,
    VerificationProvider Provider,
    VerificationResultStatus ResultStatus,
    string? UserId,
    Guid? OrganizationId,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event emitted when Corporate / CAC business verification completes.
/// </summary>
public sealed record BusinessVerificationCompletedDomainEvent(
    Guid OperationId,
    string Reference,
    VerificationProvider Provider,
    VerificationResultStatus ResultStatus,
    Guid OrganizationId,
    DateTime OccurredOnUtc);
