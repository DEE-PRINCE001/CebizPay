using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Thrift.Entities;

/// <summary>
/// Domain entity recording a net contribution reimbursement distributed to a departing thrift member within 24 hours.
/// Enforces idempotent single reimbursement per member via unique constraint on (MemberId).
/// Net Contribution = TotalContributed - TotalPayoutReceived.
/// </summary>
public class ThriftReimbursement
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Parent Thrift group ID.</summary>
    public Guid ThriftGroupId { get; private set; }

    /// <summary>Thrift member ID.</summary>
    public Guid MemberId { get; private set; }

    /// <summary>Identity user ID of the departing member.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>Net contribution refund amount.</summary>
    public decimal NetRefundAmount { get; private set; }

    /// <summary>Transactional currency.</summary>
    public Currency Currency { get; private set; }

    /// <summary>Central double-entry ledger transaction ID.</summary>
    public Guid LedgerTransactionId { get; private set; }

    /// <summary>Idempotency key for repeat-safe financial execution.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>Reimbursement execution timestamp.</summary>
    public DateTime ReimbursedAtUtc { get; private set; }

    private ThriftReimbursement() { } // EF Core

    /// <summary>
    /// Creates a new thrift reimbursement record.
    /// </summary>
    public static ThriftReimbursement Create(
        Guid thriftGroupId,
        Guid memberId,
        string userId,
        decimal netRefundAmount,
        Currency currency,
        Guid ledgerTransactionId,
        string idempotencyKey,
        DateTime reimbursedAtUtc)
    {
        if (thriftGroupId == Guid.Empty)
            throw new ArgumentException("ThriftGroupId is required.", nameof(thriftGroupId));
        if (memberId == Guid.Empty)
            throw new ArgumentException("MemberId is required.", nameof(memberId));
        if (netRefundAmount <= 0)
            throw new ArgumentException("NetRefundAmount must be positive.", nameof(netRefundAmount));

        return new ThriftReimbursement
        {
            Id = Guid.NewGuid(),
            ThriftGroupId = thriftGroupId,
            MemberId = memberId,
            UserId = userId,
            NetRefundAmount = netRefundAmount,
            Currency = currency,
            LedgerTransactionId = ledgerTransactionId,
            IdempotencyKey = idempotencyKey.Trim(),
            ReimbursedAtUtc = reimbursedAtUtc
        };
    }
}
