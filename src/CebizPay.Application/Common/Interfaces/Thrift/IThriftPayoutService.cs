namespace CebizPay.Application.Common.Interfaces.Thrift;

/// <summary>
/// Service contract responsible for cycle payout distribution to designated beneficiary wallets
/// based on actual successfully collected pools.
/// </summary>
public interface IThriftPayoutService
{
    /// <summary>
    /// Evaluates and processes all ready-for-payout cycles across active thrift groups.
    /// (Invoked by background worker).
    /// </summary>
    Task<int> ProcessReadyPayoutsAsync(DateTime asOfUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes payout for a specific cycle to the beneficiary wallet.
    /// </summary>
    Task<ThriftCycleDto> ExecuteCyclePayoutAsync(Guid cycleId, string? idempotencyKey = null, CancellationToken cancellationToken = default);
}
