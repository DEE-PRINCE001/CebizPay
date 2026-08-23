using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Loans.Events;
using CebizPay.Domain.Thrift.Enums;

namespace CebizPay.Domain.Thrift.Events;

/// <summary>Domain event emitted when a new thrift group is created.</summary>
public sealed record ThriftGroupCreatedDomainEvent(
    Guid ThriftGroupId,
    Guid? OrganizationId,
    string CreatorUserId,
    string Name,
    Currency Currency,
    decimal ContributionAmount,
    ThriftFrequency Frequency,
    int TotalPositions) : IDomainEvent;

/// <summary>Domain event emitted when a user joins a thrift group.</summary>
public sealed record ThriftMemberJoinedDomainEvent(
    Guid ThriftGroupId,
    Guid MemberId,
    string UserId) : IDomainEvent;

/// <summary>Domain event emitted when a member selects a payout position.</summary>
public sealed record ThriftPositionSelectedDomainEvent(
    Guid ThriftGroupId,
    Guid MemberId,
    string UserId,
    int Position) : IDomainEvent;

/// <summary>Domain event emitted when payout positions are locked.</summary>
public sealed record ThriftPositionsLockedDomainEvent(
    Guid ThriftGroupId,
    int TotalPositions) : IDomainEvent;

/// <summary>Domain event emitted when a rotation cycle starts.</summary>
public sealed record ThriftCycleStartedDomainEvent(
    Guid ThriftGroupId,
    Guid CycleId,
    int CycleNumber,
    string BeneficiaryUserId,
    decimal ExpectedPool) : IDomainEvent;

/// <summary>Domain event emitted when a member contribution is collected.</summary>
public sealed record ThriftContributionCollectedDomainEvent(
    Guid ThriftGroupId,
    Guid CycleId,
    Guid MemberId,
    string UserId,
    decimal Amount,
    Currency Currency,
    ThriftContributionSource Source,
    Guid? LedgerTransactionId) : IDomainEvent;

/// <summary>Domain event emitted when a member contribution is missed.</summary>
public sealed record ThriftContributionMissedDomainEvent(
    Guid ThriftGroupId,
    Guid CycleId,
    Guid MemberId,
    string UserId,
    int ConsecutiveMissedCycles) : IDomainEvent;

/// <summary>Domain event emitted when a member's payout is suspended.</summary>
public sealed record ThriftMemberSuspendedDomainEvent(
    Guid ThriftGroupId,
    Guid MemberId,
    string UserId,
    int ConsecutiveMissedCycles) : IDomainEvent;

/// <summary>Domain event emitted when a cycle payout pool is distributed to the beneficiary.</summary>
public sealed record ThriftPayoutCompletedDomainEvent(
    Guid ThriftGroupId,
    Guid CycleId,
    string BeneficiaryUserId,
    decimal Amount,
    Currency Currency,
    Guid LedgerTransactionId) : IDomainEvent;

/// <summary>Domain event emitted when a departing member is reimbursed net contributions.</summary>
public sealed record ThriftMemberReimbursedDomainEvent(
    Guid ThriftGroupId,
    Guid MemberId,
    string UserId,
    decimal NetRefundAmount,
    Currency Currency,
    Guid LedgerTransactionId) : IDomainEvent;
