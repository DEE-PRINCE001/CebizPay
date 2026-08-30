using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Finance.Events;

/// <summary>
/// Domain event published when a new platform fee policy version is created and activated.
/// </summary>
public sealed record PlatformFeePolicyCreatedDomainEvent(
    Guid PolicyId,
    FeeOperationType OperationType,
    int Version,
    FeeCalculationMethod CalculationMethod,
    FeeBearer FeeBearer,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a platform fee policy version is deactivated / superseded.
/// </summary>
public sealed record PlatformFeePolicyDeactivatedDomainEvent(
    Guid PolicyId,
    FeeOperationType OperationType,
    int Version,
    DateTime OccurredOnUtc);
