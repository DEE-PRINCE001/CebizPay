using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Thrift.Enums;

namespace CebizPay.Domain.Thrift.Entities;

/// <summary>
/// Domain entity tracking a scheduled cycle contribution from an individual thrift member.
/// Enforces idempotent repeat-safe collection via unique constraint on (ThriftCycleId, MemberId).
/// </summary>
public class ThriftContribution
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Parent rotation cycle ID.</summary>
    public Guid ThriftCycleId { get; private set; }

    /// <summary>Parent Thrift group ID.</summary>
    public Guid ThriftGroupId { get; private set; }

    /// <summary>Thrift member entity ID.</summary>
    public Guid MemberId { get; private set; }

    /// <summary>Identity user ID of the contributing member.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>Contribution amount.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Transactional currency.</summary>
    public Currency Currency { get; private set; }

    /// <summary>Collection source (Wallet or CardFallback).</summary>
    public ThriftContributionSource Source { get; private set; }

    /// <summary>Collection outcome status (Pending, Successful, Missed, Failed).</summary>
    public ThriftContributionStatus Status { get; private set; } = ThriftContributionStatus.Pending;

    /// <summary>Central ledger transaction ID if wallet collection succeeded.</summary>
    public Guid? LedgerTransactionId { get; private set; }

    /// <summary>Payment provider attempt ID if card fallback was executed.</summary>
    public Guid? PaymentAttemptId { get; private set; }

    /// <summary>Idempotency key for repeat-safe financial execution.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>Collection timestamp.</summary>
    public DateTime? CollectedAtUtc { get; private set; }

    /// <summary>Failure reason if collection failed.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private ThriftContribution() { } // EF Core

    /// <summary>
    /// Creates a new successful thrift contribution record.
    /// </summary>
    public static ThriftContribution CreateSuccessful(
        Guid thriftCycleId,
        Guid thriftGroupId,
        Guid memberId,
        string userId,
        decimal amount,
        Currency currency,
        ThriftContributionSource source,
        Guid? ledgerTransactionId,
        Guid? paymentAttemptId,
        string idempotencyKey,
        DateTime collectedAtUtc)
    {
        if (thriftCycleId == Guid.Empty)
            throw new ArgumentException("ThriftCycleId is required.", nameof(thriftCycleId));
        if (memberId == Guid.Empty)
            throw new ArgumentException("MemberId is required.", nameof(memberId));
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));

        return new ThriftContribution
        {
            Id = Guid.NewGuid(),
            ThriftCycleId = thriftCycleId,
            ThriftGroupId = thriftGroupId,
            MemberId = memberId,
            UserId = userId,
            Amount = amount,
            Currency = currency,
            Source = source,
            Status = ThriftContributionStatus.Successful,
            LedgerTransactionId = ledgerTransactionId,
            PaymentAttemptId = paymentAttemptId,
            IdempotencyKey = idempotencyKey.Trim(),
            CollectedAtUtc = collectedAtUtc,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a missed contribution record when both wallet and card attempts fail.
    /// </summary>
    public static ThriftContribution CreateMissed(
        Guid thriftCycleId,
        Guid thriftGroupId,
        Guid memberId,
        string userId,
        decimal amount,
        Currency currency,
        string idempotencyKey,
        string reason)
    {
        return new ThriftContribution
        {
            Id = Guid.NewGuid(),
            ThriftCycleId = thriftCycleId,
            ThriftGroupId = thriftGroupId,
            MemberId = memberId,
            UserId = userId,
            Amount = amount,
            Currency = currency,
            Source = ThriftContributionSource.Wallet,
            Status = ThriftContributionStatus.Missed,
            IdempotencyKey = idempotencyKey.Trim(),
            FailureReason = reason,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
