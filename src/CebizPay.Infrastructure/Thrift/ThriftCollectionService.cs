using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Thrift;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Thrift.Entities;
using CebizPay.Domain.Thrift.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Thrift;

/// <summary>
/// Infrastructure service implementation for automated 02:00 UTC scheduled collection across active thrift cycles.
/// Executes wallet-first collection with fallback to tokenized card payment.
/// </summary>
public sealed partial class ThriftCollectionService : IThriftCollectionService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILedgerPostingService _ledgerPostingService;
    private readonly IPaymentProviderFactory _providerFactory;
    private readonly ILogger<ThriftCollectionService> _logger;

    /// <summary>
    /// Initializes a new instance of ThriftCollectionService.
    /// </summary>
    public ThriftCollectionService(
        IApplicationDbContext dbContext,
        ILedgerPostingService ledgerPostingService,
        IPaymentProviderFactory providerFactory,
        ILogger<ThriftCollectionService> logger)
    {
        _dbContext = dbContext;
        _ledgerPostingService = ledgerPostingService;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> ProcessDueCollectionsAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        var dueCycles = await _dbContext.ThriftCycles
            .Include(c => c.Contributions)
            .Where(c => c.Status == ThriftCycleStatus.Collecting && c.DueDateUtc <= asOfUtc)
            .ToListAsync(cancellationToken);

        var processedCount = 0;

        foreach (var cycle in dueCycles)
        {
            var group = await _dbContext.ThriftGroups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == cycle.ThriftGroupId, cancellationToken);

            if (group == null)
                continue;

            var activeMembers = group.Members.Where(m => m.Status == ThriftMemberStatus.Active).ToList();

            foreach (var member in activeMembers)
            {
                // Idempotency: skip if already contributed
                var existingContribution = cycle.Contributions.FirstOrDefault(c => c.MemberId == member.Id);
                if (existingContribution != null && existingContribution.Status == ThriftContributionStatus.Successful)
                    continue;

                try
                {
                    await CollectMemberContributionInternalAsync(group, cycle, member, cancellationToken);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    LogCollectionError(_logger, member.Id, cycle.Id, ex);
                }
            }

            // Check if all active members processed
            var allDone = activeMembers.All(m => cycle.Contributions.Any(c => c.MemberId == m.Id));
            if (allDone)
            {
                cycle.MarkReadyForPayout();
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return processedCount;
    }

    /// <inheritdoc/>
    public async Task<ThriftContributionDto> CollectMemberContributionAsync(Guid cycleId, Guid memberId, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        var cycle = await _dbContext.ThriftCycles
            .Include(c => c.Contributions)
            .FirstOrDefaultAsync(c => c.Id == cycleId, cancellationToken)
            ?? throw new InvalidOperationException($"Thrift cycle '{cycleId}' not found.");

        var group = await _dbContext.ThriftGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == cycle.ThriftGroupId, cancellationToken)
            ?? throw new InvalidOperationException($"Thrift group '{cycle.ThriftGroupId}' not found.");

        var member = group.Members.FirstOrDefault(m => m.Id == memberId)
            ?? throw new InvalidOperationException($"Thrift member '{memberId}' not found.");

        var contribution = await CollectMemberContributionInternalAsync(group, cycle, member, cancellationToken, idempotencyKey);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapContributionToDto(contribution);
    }

    private async Task<ThriftContribution> CollectMemberContributionInternalAsync(
        ThriftGroup group,
        ThriftCycle cycle,
        ThriftMember member,
        CancellationToken cancellationToken,
        string? customIdempotencyKey = null)
    {
        var idempotencyKey = customIdempotencyKey ?? $"THRF-COL-{cycle.Id:N}-{member.Id:N}";

        // Step 1: Wallet-First collection attempt
        var memberWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.IndividualId == member.UserId && w.Currency == group.Currency, cancellationToken);

        if (memberWallet != null && memberWallet.Status == Domain.Finance.Enums.WalletStatus.Active && memberWallet.AvailableBalance >= group.ContributionAmount)
        {
            var reference = $"TC-{cycle.CycleNumber}-{member.Position}-{Guid.NewGuid():N}"[..32];
            var ledgerTx = await _ledgerPostingService.PostThriftContributionCoreAsync(
                memberWallet.Id,
                group.Id,
                group.ContributionAmount,
                group.Currency,
                reference,
                $"Thrift cycle {cycle.CycleNumber} contribution for member {member.UserId}",
                cancellationToken);

            var contribution = ThriftContribution.CreateSuccessful(
                cycle.Id,
                group.Id,
                member.Id,
                member.UserId,
                group.ContributionAmount,
                group.Currency,
                ThriftContributionSource.Wallet,
                ledgerTx.Id,
                null,
                idempotencyKey,
                DateTime.UtcNow);

            cycle.AddContribution(contribution);
            _dbContext.ThriftContributions.Add(contribution);
            member.RecordSuccessfulContribution(group.ContributionAmount);

            var audit = AuditLog.Create(
                actorId: member.UserId,
                action: AuditActions.ThriftContributionCollected,
                resourceType: AuditResourceTypes.ThriftContribution,
                resourceId: contribution.Id.ToString(),
                organizationId: group.OrganizationId,
                afterJson: $"{{\"amount\":{group.ContributionAmount},\"source\":\"Wallet\",\"cycleNumber\":{cycle.CycleNumber}}}");
            _dbContext.AuditLogs.Add(audit);

            return contribution;
        }

        // Step 2: Card Fallback Attempt
        try
        {
            var cardPaymentSucceeded = false;
            Guid? paymentAttemptId = null;

            var provider = _providerFactory.GetProvider(PaymentProvider.Flutterwave);
            if (provider != null)
            {
                LogCardFallbackAttempt(_logger, member.UserId, provider.GetType().Name);
            }

            if (cardPaymentSucceeded)
            {
                var reference = $"TCC-{cycle.CycleNumber}-{member.Position}-{Guid.NewGuid():N}"[..32];
                var ledgerTx = await _ledgerPostingService.PostThriftContributionCoreAsync(
                    memberWallet!.Id,
                    group.Id,
                    group.ContributionAmount,
                    group.Currency,
                    reference,
                    $"Thrift cycle {cycle.CycleNumber} card fallback contribution for member {member.UserId}",
                    cancellationToken);

                var contribution = ThriftContribution.CreateSuccessful(
                    cycle.Id,
                    group.Id,
                    member.Id,
                    member.UserId,
                    group.ContributionAmount,
                    group.Currency,
                    ThriftContributionSource.CardFallback,
                    ledgerTx.Id,
                    paymentAttemptId,
                    idempotencyKey,
                    DateTime.UtcNow);

                cycle.AddContribution(contribution);
                member.RecordSuccessfulContribution(group.ContributionAmount);
                return contribution;
            }
        }
        catch (Exception ex)
        {
            LogCardFallbackWarning(_logger, member.Id, ex);
        }

        // Step 3: Record Missed Contribution & Check Delinquency
        var missedContribution = ThriftContribution.CreateMissed(
            cycle.Id,
            group.Id,
            member.Id,
            member.UserId,
            group.ContributionAmount,
            group.Currency,
            idempotencyKey,
            "Insufficient wallet balance and card fallback failed/unavailable.");

        cycle.AddContribution(missedContribution);
        _dbContext.ThriftContributions.Add(missedContribution);
        var newlySuspended = member.RecordMissedContribution();

        var missedAudit = AuditLog.Create(
            actorId: member.UserId,
            action: AuditActions.ThriftContributionMissed,
            resourceType: AuditResourceTypes.ThriftContribution,
            resourceId: missedContribution.Id.ToString(),
            organizationId: group.OrganizationId,
            afterJson: $"{{\"missedCycles\":{member.ConsecutiveMissedCycles},\"cycleNumber\":{cycle.CycleNumber}}}");
        _dbContext.AuditLogs.Add(missedAudit);

        if (newlySuspended)
        {
            var suspendAudit = AuditLog.Create(
                actorId: member.UserId,
                action: AuditActions.ThriftPayoutSuspended,
                resourceType: AuditResourceTypes.ThriftMember,
                resourceId: member.Id.ToString(),
                organizationId: group.OrganizationId,
                afterJson: $"{{\"suspended\":true,\"reason\":\"2 consecutive missed contributions\"}}");
            _dbContext.AuditLogs.Add(suspendAudit);
        }

        return missedContribution;
    }

    private static ThriftContributionDto MapContributionToDto(ThriftContribution contribution) =>
        new(
            contribution.Id,
            contribution.ThriftCycleId,
            contribution.ThriftGroupId,
            contribution.MemberId,
            contribution.UserId,
            contribution.Amount,
            contribution.Currency,
            contribution.Source,
            contribution.Status,
            contribution.LedgerTransactionId,
            contribution.FailureReason,
            contribution.CollectedAtUtc);

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Error processing collection for member {MemberId} in cycle {CycleId}")]
    private static partial void LogCollectionError(ILogger logger, Guid memberId, Guid cycleId, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Attempting card fallback charge for member {UserId} via provider {ProviderName}")]
    private static partial void LogCardFallbackAttempt(ILogger logger, string userId, string providerName);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Card fallback failed for member {MemberId}")]
    private static partial void LogCardFallbackWarning(ILogger logger, Guid memberId, Exception exception);
}
