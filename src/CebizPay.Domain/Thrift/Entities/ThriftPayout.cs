using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Thrift.Entities;

/// <summary>
/// Domain entity recording an authoritative pool payout distribution from a thrift cycle to a beneficiary.
/// Enforces idempotent single payout per cycle via unique constraint on (ThriftCycleId).
/// </summary>
public class ThriftPayout
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Parent rotation cycle ID.</summary>
    public Guid ThriftCycleId { get; private set; }

    /// <summary>Parent Thrift group ID.</summary>
    public Guid ThriftGroupId { get; private set; }

    /// <summary>Identity user ID of the receiving beneficiary.</summary>
    public string BeneficiaryUserId { get; private set; } = string.Empty;

    /// <summary>Payout pool amount distributed.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Transactional currency.</summary>
    public Currency Currency { get; private set; }

    /// <summary>Central double-entry ledger transaction ID.</summary>
    public Guid LedgerTransactionId { get; private set; }

    /// <summary>Idempotency key for repeat-safe financial execution.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>Payout settlement timestamp.</summary>
    public DateTime PaidAtUtc { get; private set; }

    private ThriftPayout() { } // EF Core

    /// <summary>
    /// Creates a new thrift payout record.
    /// </summary>
    public static ThriftPayout Create(
        Guid thriftCycleId,
        Guid thriftGroupId,
        string beneficiaryUserId,
        decimal amount,
        Currency currency,
        Guid ledgerTransactionId,
        string idempotencyKey,
        DateTime paidAtUtc)
    {
        if (thriftCycleId == Guid.Empty)
            throw new ArgumentException("ThriftCycleId is required.", nameof(thriftCycleId));
        if (string.IsNullOrWhiteSpace(beneficiaryUserId))
            throw new ArgumentException("BeneficiaryUserId is required.", nameof(beneficiaryUserId));
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));

        return new ThriftPayout
        {
            Id = Guid.NewGuid(),
            ThriftCycleId = thriftCycleId,
            ThriftGroupId = thriftGroupId,
            BeneficiaryUserId = beneficiaryUserId,
            Amount = amount,
            Currency = currency,
            LedgerTransactionId = ledgerTransactionId,
            IdempotencyKey = idempotencyKey.Trim(),
            PaidAtUtc = paidAtUtc
        };
    }
}
