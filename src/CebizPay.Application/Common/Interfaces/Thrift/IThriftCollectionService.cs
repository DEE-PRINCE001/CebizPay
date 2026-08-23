namespace CebizPay.Application.Common.Interfaces.Thrift;

/// <summary>
/// Service contract responsible for 02:00 UTC automated scheduled collection across active thrift cycles,
/// executing wallet-first deduction with tokenized card fallback.
/// </summary>
public interface IThriftCollectionService
{
    /// <summary>
    /// Runs automated 02:00 UTC collection across all eligible collecting cycles.
    /// (Invoked by background worker).
    /// </summary>
    Task<int> ProcessDueCollectionsAsync(DateTime asOfUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Collects contribution for an individual member in a cycle (wallet-first with card fallback).
    /// </summary>
    Task<ThriftContributionDto> CollectMemberContributionAsync(Guid cycleId, Guid memberId, string? idempotencyKey = null, CancellationToken cancellationToken = default);
}
