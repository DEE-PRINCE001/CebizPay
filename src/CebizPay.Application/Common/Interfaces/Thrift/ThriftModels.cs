using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Thrift.Enums;

namespace CebizPay.Application.Common.Interfaces.Thrift;

/// <summary>
/// Request to create a new Thrift (Ajo / Esusu) group.
/// </summary>
public record CreateThriftGroupRequest(
    Guid? OrganizationId,
    string Name,
    string? Description,
    Currency Currency,
    decimal ContributionAmount,
    ThriftFrequency Frequency,
    int TotalPositions,
    DateTime StartDateUtc,
    DateTime PositionSelectionDeadlineUtc);

/// <summary>
/// Response DTO representing a Thrift group.
/// </summary>
public record ThriftGroupDto(
    Guid Id,
    Guid? OrganizationId,
    string CreatorUserId,
    string Name,
    string? Description,
    Currency Currency,
    decimal ContributionAmount,
    ThriftFrequency Frequency,
    int TotalPositions,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    DateTime PositionSelectionDeadlineUtc,
    ThriftStatus Status,
    int CurrentCycleNumber,
    int TotalMembersCount,
    DateTime CreatedAtUtc);

/// <summary>
/// Request to invite a member to a thrift group.
/// </summary>
public record InviteThriftMemberRequest(
    string? Email = null);

/// <summary>
/// Response DTO representing a Thrift Invitation.
/// </summary>
public record ThriftInvitationDto(
    Guid Id,
    Guid ThriftGroupId,
    string? Email,
    string InvitationCode,
    DateTime ExpiresAtUtc,
    bool IsAccepted,
    string InvitedByUserId,
    string? AcceptedByUserId,
    DateTime? AcceptedAtUtc,
    DateTime CreatedAtUtc);

/// <summary>
/// Request to accept a thrift invitation using an invitation code.
/// </summary>
public record AcceptThriftInvitationRequest(
    string InvitationCode);

/// <summary>
/// Response DTO representing a Thrift member.
/// </summary>
public record ThriftMemberDto(
    Guid Id,
    Guid ThriftGroupId,
    string UserId,
    int? Position,
    ThriftMemberStatus Status,
    int ConsecutiveMissedCycles,
    decimal TotalContributed,
    decimal TotalPayoutReceived,
    DateTime JoinedAtUtc,
    DateTime? PositionSelectedAtUtc,
    DateTime? SuspendedAtUtc);

/// <summary>
/// Request to select an available payout rotation position.
/// </summary>
public record SelectThriftPositionRequest(
    int Position);

/// <summary>
/// Response DTO representing a Thrift rotation cycle.
/// </summary>
public record ThriftCycleDto(
    Guid Id,
    Guid ThriftGroupId,
    int CycleNumber,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    DateTime DueDateUtc,
    int TargetPayoutPosition,
    string TargetBeneficiaryUserId,
    decimal TotalExpectedPool,
    decimal TotalCollectedPool,
    ThriftCycleStatus Status,
    DateTime? PayoutCompletedAtUtc,
    Guid? PayoutLedgerTransactionId,
    DateTime CreatedAtUtc);

/// <summary>
/// Response DTO representing a member's scheduled cycle contribution.
/// </summary>
public record ThriftContributionDto(
    Guid Id,
    Guid ThriftCycleId,
    Guid ThriftGroupId,
    Guid MemberId,
    string UserId,
    decimal Amount,
    Currency Currency,
    ThriftContributionSource Source,
    ThriftContributionStatus Status,
    Guid? LedgerTransactionId,
    string? FailureReason,
    DateTime? CollectedAtUtc);

/// <summary>
/// Request to remove a member and trigger net contribution reimbursement.
/// </summary>
public record RemoveThriftMemberRequest(
    string? Reason = null);

/// <summary>
/// Response DTO returned after a departing member is reimbursed.
/// </summary>
public record ThriftReimbursementDto(
    Guid Id,
    Guid ThriftGroupId,
    Guid MemberId,
    string UserId,
    decimal NetRefundAmount,
    Currency Currency,
    Guid LedgerTransactionId,
    DateTime ReimbursedAtUtc);
