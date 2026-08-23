using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Thrift;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Thrift.Entities;
using CebizPay.Domain.Thrift.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Thrift;

/// <summary>
/// Infrastructure service implementation for distributing cycle pool payouts to designated beneficiaries.
/// </summary>
public sealed partial class ThriftPayoutService : IThriftPayoutService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILedgerPostingService _ledgerPostingService;
    private readonly ILogger<ThriftPayoutService> _logger;

    /// <summary>
    /// Initializes a new instance of ThriftPayoutService.
    /// </summary>
    public ThriftPayoutService(
        IApplicationDbContext dbContext,
        ILedgerPostingService ledgerPostingService,
        ILogger<ThriftPayoutService> logger)
    {
        _dbContext = dbContext;
        _ledgerPostingService = ledgerPostingService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> ProcessReadyPayoutsAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        var readyCycles = await _dbContext.ThriftCycles
            .Where(c => c.Status == ThriftCycleStatus.ReadyForPayout)
            .ToListAsync(cancellationToken);

        var processedCount = 0;

        foreach (var cycle in readyCycles)
        {
            try
            {
                await ExecuteCyclePayoutInternalAsync(cycle, null, cancellationToken);
                processedCount++;
            }
            catch (Exception ex)
            {
                LogPayoutError(_logger, cycle.Id, ex);
            }
        }

        return processedCount;
    }

    /// <inheritdoc/>
    public async Task<ThriftCycleDto> ExecuteCyclePayoutAsync(Guid cycleId, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        var cycle = await _dbContext.ThriftCycles
            .FirstOrDefaultAsync(c => c.Id == cycleId, cancellationToken)
            ?? throw new InvalidOperationException($"Thrift cycle '{cycleId}' not found.");

        await ExecuteCyclePayoutInternalAsync(cycle, idempotencyKey, cancellationToken);

        return MapCycleToDto(cycle);
    }

    private async Task ExecuteCyclePayoutInternalAsync(
        ThriftCycle cycle,
        string? customIdempotencyKey,
        CancellationToken cancellationToken)
    {
        if (cycle.Status == ThriftCycleStatus.Paid)
            return; // Already paid idempotently

        var group = await _dbContext.ThriftGroups
            .Include(g => g.Members)
            .Include(g => g.Cycles)
            .FirstOrDefaultAsync(g => g.Id == cycle.ThriftGroupId, cancellationToken)
            ?? throw new InvalidOperationException($"Thrift group '{cycle.ThriftGroupId}' not found.");

        var beneficiaryMember = group.Members.FirstOrDefault(m => m.UserId == cycle.TargetBeneficiaryUserId && m.Position == cycle.TargetPayoutPosition)
            ?? throw new InvalidOperationException($"Beneficiary member not found for position {cycle.TargetPayoutPosition}.");

        // Payout eligibility check: 2 consecutive missed contributions suspends payout
        if (beneficiaryMember.Status == ThriftMemberStatus.Suspended)
        {
            LogPayoutSuspendedWarning(_logger, cycle.Id, beneficiaryMember.UserId);
            var suspendAudit = AuditLog.Create(
                actorId: "SYSTEM",
                action: AuditActions.ThriftPayoutSuspended,
                resourceType: AuditResourceTypes.ThriftCycle,
                resourceId: cycle.Id.ToString(),
                organizationId: group.OrganizationId,
                afterJson: $"{{\"suspended\":true,\"beneficiary\":\"{beneficiaryMember.UserId}\",\"cycleNumber\":{cycle.CycleNumber}}}");
            _dbContext.AuditLogs.Add(suspendAudit);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var payoutAmount = cycle.TotalCollectedPool;
        var nowUtc = DateTime.UtcNow;
        Guid ledgerTxId = Guid.Empty;

        if (payoutAmount > 0)
        {
            var benWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.IndividualId == beneficiaryMember.UserId && w.Currency == group.Currency, cancellationToken)
                ?? throw new InvalidOperationException($"Beneficiary wallet not found for currency '{group.Currency}'.");

            var reference = $"TP-{cycle.CycleNumber}-{Guid.NewGuid():N}"[..32];
            var ledgerTx = await _ledgerPostingService.PostThriftPayoutCoreAsync(
                benWallet.Id,
                group.Id,
                payoutAmount,
                group.Currency,
                reference,
                $"Payout for Thrift group '{group.Name}' cycle {cycle.CycleNumber}",
                cancellationToken);

            ledgerTxId = ledgerTx.Id;
            beneficiaryMember.RecordPayout(payoutAmount);
        }

        var idempotencyKey = customIdempotencyKey ?? $"THRF-PAYOUT-{cycle.Id:N}";
        var payoutRecord = ThriftPayout.Create(
            cycle.Id,
            group.Id,
            beneficiaryMember.UserId,
            payoutAmount,
            group.Currency,
            ledgerTxId == Guid.Empty ? Guid.NewGuid() : ledgerTxId,
            idempotencyKey,
            nowUtc);

        _dbContext.ThriftPayouts.Add(payoutRecord);
        cycle.MarkPaid(ledgerTxId == Guid.Empty ? Guid.NewGuid() : ledgerTxId, nowUtc);

        // Check if group is completed or start next cycle
        if (cycle.CycleNumber >= group.TotalPositions)
        {
            group.CompleteGroup();
        }
        else
        {
            var nextCycleNumber = cycle.CycleNumber + 1;
            var nextStartDate = cycle.EndDateUtc;
            var nextEndDate = group.Frequency switch
            {
                ThriftFrequency.Daily => nextStartDate.AddDays(1),
                ThriftFrequency.Weekly => nextStartDate.AddDays(7),
                ThriftFrequency.Monthly => nextStartDate.AddMonths(1),
                _ => nextStartDate.AddMonths(1)
            };

            var nextCycle = group.StartCycle(nextCycleNumber, nextStartDate, nextEndDate, nextEndDate);
            _dbContext.ThriftCycles.Add(nextCycle);
        }

        var audit = AuditLog.Create(
            actorId: "SYSTEM",
            action: AuditActions.ThriftPayoutCompleted,
            resourceType: AuditResourceTypes.ThriftPayout,
            resourceId: payoutRecord.Id.ToString(),
            organizationId: group.OrganizationId,
            afterJson: $"{{\"payoutAmount\":{payoutAmount},\"currency\":\"{group.Currency}\",\"beneficiary\":\"{beneficiaryMember.UserId}\",\"cycleNumber\":{cycle.CycleNumber}}}");
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

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

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Error processing payout for cycle {CycleId}")]
    private static partial void LogPayoutError(ILogger logger, Guid cycleId, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Payout for cycle {CycleId} suspended because beneficiary {UserId} has 2 consecutive missed contributions")]
    private static partial void LogPayoutSuspendedWarning(ILogger logger, Guid cycleId, string userId);
}
