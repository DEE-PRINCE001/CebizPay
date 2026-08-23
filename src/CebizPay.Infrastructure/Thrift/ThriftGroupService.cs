using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Thrift;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Thrift.Entities;
using CebizPay.Domain.Thrift.Enums;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Thrift;

/// <summary>
/// Infrastructure service implementation for Thrift group management, member invitations, joins, position selection, and departures.
/// </summary>
public class ThriftGroupService : IThriftGroupService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILedgerPostingService _ledgerPostingService;

    /// <summary>
    /// Initializes a new instance of ThriftGroupService.
    /// </summary>
    public ThriftGroupService(
        IApplicationDbContext dbContext,
        ILedgerPostingService ledgerPostingService)
    {
        _dbContext = dbContext;
        _ledgerPostingService = ledgerPostingService;
    }

    /// <inheritdoc/>
    public async Task<ThriftGroupDto> CreateGroupAsync(string creatorUserId, CreateThriftGroupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(creatorUserId))
            throw new ArgumentException("CreatorUserId is required.", nameof(creatorUserId));

        var group = ThriftGroup.Create(
            request.OrganizationId,
            creatorUserId,
            request.Name,
            request.Description,
            request.Currency,
            request.ContributionAmount,
            request.Frequency,
            request.TotalPositions,
            request.StartDateUtc,
            request.PositionSelectionDeadlineUtc);

        _dbContext.ThriftGroups.Add(group);
        _dbContext.ThriftMembers.Add(group.Members.First());

        var audit = AuditLog.Create(
            actorId: creatorUserId,
            action: AuditActions.ThriftCreated,
            resourceType: AuditResourceTypes.ThriftGroup,
            resourceId: group.Id.ToString(),
            organizationId: group.OrganizationId,
            afterJson: $"{{\"name\":\"{group.Name}\",\"positions\":{group.TotalPositions},\"amount\":{group.ContributionAmount}}}");
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapGroupToDto(group);
    }

    /// <inheritdoc/>
    public async Task<ThriftGroupDto?> GetGroupByIdAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var group = await _dbContext.ThriftGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

        return group == null ? null : MapGroupToDto(group);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ThriftGroupDto>> GetGroupsAsync(string? userId = null, Guid? organizationId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ThriftGroups.Include(g => g.Members).AsQueryable();

        if (organizationId.HasValue)
        {
            query = query.Where(g => g.OrganizationId == organizationId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(g => g.CreatorUserId == userId || g.Members.Any(m => m.UserId == userId));
        }

        var groups = await query.OrderByDescending(g => g.CreatedAtUtc).ToListAsync(cancellationToken);
        return groups.Select(MapGroupToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<ThriftInvitationDto> InviteMemberAsync(Guid groupId, string invitedByUserId, InviteThriftMemberRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var group = await _dbContext.ThriftGroups
            .Include(g => g.Invitations)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new InvalidOperationException($"Thrift group '{groupId}' not found.");

        var invitation = group.CreateInvitation(request.Email, invitedByUserId);
        _dbContext.ThriftInvitations.Add(invitation);

        var audit = AuditLog.Create(
            actorId: invitedByUserId,
            action: AuditActions.ThriftMemberInvited,
            resourceType: AuditResourceTypes.ThriftInvitation,
            resourceId: invitation.Id.ToString(),
            organizationId: group.OrganizationId,
            afterJson: $"{{\"code\":\"{invitation.InvitationCode}\",\"groupId\":\"{group.Id}\"}}");
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapInvitationToDto(invitation);
    }

    /// <inheritdoc/>
    public async Task<ThriftMemberDto> AcceptInvitationAsync(string userId, AcceptThriftInvitationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(request.InvitationCode))
            throw new ArgumentException("InvitationCode is required.", nameof(request));

        var cleanCode = request.InvitationCode.Trim().ToUpperInvariant();
        var invitation = await _dbContext.ThriftInvitations
            .FirstOrDefaultAsync(i => i.InvitationCode == cleanCode, cancellationToken)
            ?? throw new InvalidOperationException("Invalid or non-existent invitation code.");

        var group = await _dbContext.ThriftGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == invitation.ThriftGroupId, cancellationToken)
            ?? throw new InvalidOperationException($"Thrift group '{invitation.ThriftGroupId}' not found.");

        invitation.Accept(userId);
        var member = group.AddMember(userId);
        _dbContext.ThriftMembers.Add(member);

        var audit = AuditLog.Create(
            actorId: userId,
            action: AuditActions.ThriftMemberJoined,
            resourceType: AuditResourceTypes.ThriftMember,
            resourceId: member.Id.ToString(),
            organizationId: group.OrganizationId,
            afterJson: $"{{\"userId\":\"{userId}\",\"groupId\":\"{group.Id}\"}}");
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapMemberToDto(member);
    }

    /// <inheritdoc/>
    public async Task<ThriftMemberDto> SelectPositionAsync(Guid groupId, string userId, SelectThriftPositionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Position <= 0)
            throw new ArgumentException("Position must be a positive integer.", nameof(request));

        var group = await _dbContext.ThriftGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new InvalidOperationException($"Thrift group '{groupId}' not found.");

        if (group.Status == ThriftStatus.Locked || group.Status == ThriftStatus.Active || group.Status == ThriftStatus.Completed)
            throw new InvalidOperationException("Cannot change position: Thrift group positions are already locked.");

        if (request.Position > group.TotalPositions)
            throw new ArgumentException($"Position {request.Position} exceeds total available positions ({group.TotalPositions}).", nameof(request));

        var member = group.Members.FirstOrDefault(m => m.UserId == userId && m.Status == ThriftMemberStatus.Active)
            ?? throw new InvalidOperationException("You are not an active member of this thrift group.");

        // Check if position is already taken by another member
        var isTaken = group.Members.Any(m => m.Position == request.Position && m.Id != member.Id && m.Status == ThriftMemberStatus.Active);
        if (isTaken)
            throw new InvalidOperationException($"Position {request.Position} has already been claimed by another member.");

        member.SelectPosition(request.Position);

        // Check if all positions are now filled to automatically lock positions
        var activeMembers = group.Members.Where(m => m.Status == ThriftMemberStatus.Active).ToList();
        if (activeMembers.Count == group.TotalPositions && activeMembers.All(m => m.Position.HasValue))
        {
            group.LockPositions();
            var lockAudit = AuditLog.Create(
                actorId: userId,
                action: AuditActions.ThriftPositionsLocked,
                resourceType: AuditResourceTypes.ThriftGroup,
                resourceId: group.Id.ToString(),
                organizationId: group.OrganizationId,
                afterJson: $"{{\"totalPositions\":{group.TotalPositions},\"status\":\"{group.Status}\"}}");
            _dbContext.AuditLogs.Add(lockAudit);
        }

        var audit = AuditLog.Create(
            actorId: userId,
            action: AuditActions.ThriftPositionSelected,
            resourceType: AuditResourceTypes.ThriftMember,
            resourceId: member.Id.ToString(),
            organizationId: group.OrganizationId,
            afterJson: $"{{\"position\":{request.Position},\"userId\":\"{userId}\"}}");
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapMemberToDto(member);
    }

    /// <inheritdoc/>
    public async Task<ThriftGroupDto> LockPositionsAsync(Guid groupId, string requestedByUserId, CancellationToken cancellationToken = default)
    {
        var group = await _dbContext.ThriftGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new InvalidOperationException($"Thrift group '{groupId}' not found.");

        if (group.CreatorUserId != requestedByUserId)
            throw new UnauthorizedAccessException("Only the group creator can manually lock positions.");

        group.LockPositions();

        var audit = AuditLog.Create(
            actorId: requestedByUserId,
            action: AuditActions.ThriftPositionsLocked,
            resourceType: AuditResourceTypes.ThriftGroup,
            resourceId: group.Id.ToString(),
            organizationId: group.OrganizationId,
            afterJson: $"{{\"totalPositions\":{group.TotalPositions},\"status\":\"{group.Status}\"}}");
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapGroupToDto(group);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ThriftMemberDto>> GetGroupMembersAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var members = await _dbContext.ThriftMembers
            .Where(m => m.ThriftGroupId == groupId)
            .OrderBy(m => m.Position ?? int.MaxValue)
            .ThenBy(m => m.JoinedAtUtc)
            .ToListAsync(cancellationToken);

        return members.Select(MapMemberToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ThriftCycleDto>> GetGroupCyclesAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var cycles = await _dbContext.ThriftCycles
            .Where(c => c.ThriftGroupId == groupId)
            .OrderBy(c => c.CycleNumber)
            .ToListAsync(cancellationToken);

        return cycles.Select(MapCycleToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<ThriftReimbursementDto> RemoveAndReimburseMemberAsync(
        Guid groupId,
        Guid memberId,
        string requestedByUserId,
        RemoveThriftMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var group = await _dbContext.ThriftGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new InvalidOperationException($"Thrift group '{groupId}' not found.");

        var member = group.Members.FirstOrDefault(m => m.Id == memberId)
            ?? throw new InvalidOperationException($"Member '{memberId}' not found in thrift group.");

        if (member.UserId != requestedByUserId && group.CreatorUserId != requestedByUserId)
            throw new UnauthorizedAccessException("You are not authorized to remove this member.");

        var netRefundAmount = member.CalculateNetRefundableAmount();
        var nowUtc = DateTime.UtcNow;

        Guid ledgerTxId = Guid.Empty;
        if (netRefundAmount > 0)
        {
            var memberWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.IndividualId == member.UserId && w.Currency == group.Currency, cancellationToken)
                ?? throw new InvalidOperationException($"Member wallet not found for currency '{group.Currency}'.");

            var reference = $"TR-{Guid.NewGuid():N}"[..32];
            var ledgerTx = await _ledgerPostingService.PostThriftReimbursementCoreAsync(
                memberWallet.Id,
                group.Id,
                netRefundAmount,
                group.Currency,
                reference,
                $"Net contribution refund for departing thrift member {member.Id}",
                cancellationToken);

            ledgerTxId = ledgerTx.Id;
        }

        member.MarkRefunded();

        var idempotencyKey = $"THRF-REIMB-{member.Id:N}";
        var reimbursement = ThriftReimbursement.Create(
            group.Id,
            member.Id,
            member.UserId,
            Math.Max(netRefundAmount, 0.01m),
            group.Currency,
            ledgerTxId == Guid.Empty ? Guid.NewGuid() : ledgerTxId,
            idempotencyKey,
            nowUtc);

        _dbContext.ThriftReimbursements.Add(reimbursement);

        var audit = AuditLog.Create(
            actorId: requestedByUserId,
            action: AuditActions.ThriftMemberReimbursed,
            resourceType: AuditResourceTypes.ThriftReimbursement,
            resourceId: reimbursement.Id.ToString(),
            organizationId: group.OrganizationId,
            afterJson: $"{{\"netRefundAmount\":{netRefundAmount},\"currency\":\"{group.Currency}\",\"memberId\":\"{member.Id}\"}}");
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ThriftReimbursementDto(
            reimbursement.Id,
            reimbursement.ThriftGroupId,
            reimbursement.MemberId,
            reimbursement.UserId,
            reimbursement.NetRefundAmount,
            reimbursement.Currency,
            reimbursement.LedgerTransactionId,
            reimbursement.ReimbursedAtUtc);
    }

    private static ThriftGroupDto MapGroupToDto(ThriftGroup group) =>
        new(
            group.Id,
            group.OrganizationId,
            group.CreatorUserId,
            group.Name,
            group.Description,
            group.Currency,
            group.ContributionAmount,
            group.Frequency,
            group.TotalPositions,
            group.StartDateUtc,
            group.EndDateUtc,
            group.PositionSelectionDeadlineUtc,
            group.Status,
            group.CurrentCycleNumber,
            group.Members.Count,
            group.CreatedAtUtc);

    private static ThriftInvitationDto MapInvitationToDto(ThriftInvitation invitation) =>
        new(
            invitation.Id,
            invitation.ThriftGroupId,
            invitation.Email,
            invitation.InvitationCode,
            invitation.ExpiresAtUtc,
            invitation.IsAccepted,
            invitation.InvitedByUserId,
            invitation.AcceptedByUserId,
            invitation.AcceptedAtUtc,
            invitation.CreatedAtUtc);

    private static ThriftMemberDto MapMemberToDto(ThriftMember member) =>
        new(
            member.Id,
            member.ThriftGroupId,
            member.UserId,
            member.Position,
            member.Status,
            member.ConsecutiveMissedCycles,
            member.TotalContributed,
            member.TotalPayoutReceived,
            member.JoinedAtUtc,
            member.PositionSelectedAtUtc,
            member.SuspendedAtUtc);

    private static ThriftCycleDto MapCycleToDto(ThriftCycle cycle) =>
        new(
            cycle.Id,
            cycle.ThriftGroupId,
            cycle.CycleNumber,
            cycle.StartDateUtc,
            cycle.EndDateUtc,
            cycle.DueDateUtc,
            cycle.TargetPayoutPosition,
            cycle.TargetBeneficiaryUserId,
            cycle.TotalExpectedPool,
            cycle.TotalCollectedPool,
            cycle.Status,
            cycle.PayoutCompletedAtUtc,
            cycle.PayoutLedgerTransactionId,
            cycle.CreatedAtUtc);
}
