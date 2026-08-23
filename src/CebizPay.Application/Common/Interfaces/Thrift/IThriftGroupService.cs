namespace CebizPay.Application.Common.Interfaces.Thrift;

/// <summary>
/// Service contract managing Thrift group creation, member invitations, joins, position selection, and position locking.
/// </summary>
public interface IThriftGroupService
{
    /// <summary>
    /// Creates a new Thrift group in OpenForMembers status.
    /// </summary>
    Task<ThriftGroupDto> CreateGroupAsync(string creatorUserId, CreateThriftGroupRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a Thrift group by ID.
    /// </summary>
    Task<ThriftGroupDto?> GetGroupByIdAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists thrift groups available for or joined by a user.
    /// </summary>
    Task<IReadOnlyList<ThriftGroupDto>> GetGroupsAsync(string? userId = null, Guid? organizationId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues an invitation code for a prospective member.
    /// </summary>
    Task<ThriftInvitationDto> InviteMemberAsync(Guid groupId, string invitedByUserId, InviteThriftMemberRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts a thrift invitation code and joins the group.
    /// </summary>
    Task<ThriftMemberDto> AcceptInvitationAsync(string userId, AcceptThriftInvitationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Selects an available payout rotation position. Enforces PostgreSQL concurrency uniqueness.
    /// </summary>
    Task<ThriftMemberDto> SelectPositionAsync(Guid groupId, string userId, SelectThriftPositionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Locks payout positions and prepares the group for cycle activation once all positions are filled.
    /// </summary>
    Task<ThriftGroupDto> LockPositionsAsync(Guid groupId, string requestedByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists participating members in a thrift group.
    /// </summary>
    Task<IReadOnlyList<ThriftMemberDto>> GetGroupMembersAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists rotation cycles in a thrift group.
    /// </summary>
    Task<IReadOnlyList<ThriftCycleDto>> GetGroupCyclesAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a departing member and schedules net contribution reimbursement.
    /// </summary>
    Task<ThriftReimbursementDto> RemoveAndReimburseMemberAsync(Guid groupId, Guid memberId, string requestedByUserId, RemoveThriftMemberRequest request, CancellationToken cancellationToken = default);
}
