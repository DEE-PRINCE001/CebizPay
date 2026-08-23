namespace CebizPay.Domain.Savings.Entities;

/// <summary>
/// Domain entity recording an authoritative daily interest accrual calculation on a savings account.
/// Enforces idempotent repeat-safe execution via unique constraint on (SavingsAccountId, AccrualDate).
/// </summary>
public class SavingsInterestAccrual
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Parent savings account ID.</summary>
    public Guid SavingsAccountId { get; private set; }

    /// <summary>Calendar accrual date (date-only component).</summary>
    public DateTime AccrualDate { get; private set; }

    /// <summary>Principal balance basis evaluated on this accrual date.</summary>
    public decimal PrincipalBasis { get; private set; }

    /// <summary>Annual interest rate applied for calculation.</summary>
    public decimal Rate { get; private set; }

    /// <summary>Daily accrued interest amount computed for this date.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Governing interest policy version number.</summary>
    public int PolicyVersion { get; private set; }

    /// <summary>Ledger transaction ID if interest was financially realized immediately, or null if accumulated.</summary>
    public Guid? LedgerTransactionId { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private SavingsInterestAccrual() { } // EF Core

    /// <summary>
    /// Creates a new daily interest accrual record.
    /// </summary>
    public static SavingsInterestAccrual Create(
        Guid savingsAccountId,
        DateTime accrualDate,
        decimal principalBasis,
        decimal rate,
        decimal amount,
        int policyVersion,
        Guid? ledgerTransactionId = null)
    {
        if (savingsAccountId == Guid.Empty)
            throw new ArgumentException("SavingsAccountId is required.", nameof(savingsAccountId));
        if (amount < 0)
            throw new ArgumentException("Accrual amount cannot be negative.", nameof(amount));

        return new SavingsInterestAccrual
        {
            Id = Guid.NewGuid(),
            SavingsAccountId = savingsAccountId,
            AccrualDate = accrualDate.Date,
            PrincipalBasis = principalBasis,
            Rate = rate,
            Amount = amount,
            PolicyVersion = policyVersion,
            LedgerTransactionId = ledgerTransactionId,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
