using CebizPay.Application.Common.Interfaces.Thrift;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Thrift.Enums;

namespace CebizPay.Application.UseCases.Admin.ThriftOversight;

/// <summary>
/// Overview summary DTO for a Thrift group in the Super Admin oversight portal.
/// </summary>
public sealed record AdminThriftGroupSummaryDto(
    Guid Id,
    Guid? OrganizationId,
    string? OrganizationName,
    string CreatorUserId,
    string Name,
    string? Description,
    Currency Currency,
    decimal ContributionAmount,
    ThriftFrequency Frequency,
    int TotalPositions,
    int ActiveMembersCount,
    ThriftStatus Status,
    int CurrentCycleNumber,
    decimal TotalPoolVolume,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    DateTime CreatedAtUtc);

/// <summary>
/// Detailed oversight DTO for a Thrift group, including member positions, rotation cycles, and dispute records.
/// </summary>
public sealed record AdminThriftGroupDetailsDto(
    AdminThriftGroupSummaryDto Group,
    IReadOnlyList<ThriftMemberDto> Members,
    IReadOnlyList<ThriftCycleDto> Cycles,
    IReadOnlyList<ThriftDisputeDto> Disputes);

/// <summary>
/// DTO representing a delinquent or suspended Thrift member requiring oversight.
/// </summary>
public sealed record AdminThriftDelinquencyDto(
    Guid MemberId,
    Guid ThriftGroupId,
    string GroupName,
    string UserId,
    string Status,
    int ConsecutiveMissedCycles,
    decimal TotalContributed,
    decimal TotalPayoutReceived,
    DateTime JoinedAtUtc,
    DateTime? SuspendedAtUtc);

/// <summary>
/// DTO representing a Thrift oversight dispute case.
/// </summary>
public sealed record ThriftDisputeDto(
    Guid Id,
    Guid ThriftGroupId,
    string GroupName,
    Guid? CycleId,
    Guid? MemberId,
    string ReportedByUserId,
    string Reason,
    string Status,
    string? ResolutionNotes,
    string? ResolvedByUserId,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc);

/// <summary>
/// Request to submit a new dispute on a Thrift group.
/// </summary>
public sealed record CreateThriftDisputeRequest(
    Guid ThriftGroupId,
    Guid? CycleId,
    Guid? MemberId,
    string Reason);

/// <summary>
/// Request to resolve or reject an existing Thrift dispute.
/// </summary>
public sealed record ResolveThriftDisputeRequest(
    string ResolutionNotes,
    bool Reject = false);

/// <summary>
/// Request for a Super Admin to pause a Thrift group.
/// </summary>
public sealed record PauseThriftGroupRequest(
    string Reason);
