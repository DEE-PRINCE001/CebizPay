using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Savings.Enums;

namespace CebizPay.Domain.Savings.Entities;

/// <summary>
/// Domain aggregate root representing an individual or corporate savings account / subscription instance.
/// Holds the materialized operational state for balance and accrued interest calculations,
/// while all underlying monetary movements remain authoritatively recorded on the central ledger.
/// </summary>
public class SavingsAccount
{
    private readonly List<SavingsContribution> _contributions = [];
    private readonly List<SavingsInterestAccrual> _interestAccruals = [];

    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Reference to the parent SavingsPlan.</summary>
    public Guid SavingsPlanId { get; private set; }

    /// <summary>Identity user ID of the account owner / saver.</summary>
    public string OwnerUserId { get; private set; } = string.Empty;

    /// <summary>Owning organization ID for corporate plans, or null for individual plans.</summary>
    public Guid? OrganizationId { get; private set; }

    /// <summary>Transactional currency.</summary>
    public Currency Currency { get; private set; } = Currency.NGN;

    /// <summary>Savings product type.</summary>
    public SavingsPlanType PlanType { get; private set; }

    /// <summary>Current principal balance deposited into savings.</summary>
    public decimal PrincipalBalance { get; private set; }

    /// <summary>Cumulative unliquidated interest accrued to date.</summary>
    public decimal AccruedInterest { get; private set; }

    /// <summary>Total interest realized and withdrawn to date.</summary>
    public decimal TotalInterestWithdrawn { get; private set; }

    /// <summary>Current lifecycle status.</summary>
    public SavingsAccountStatus Status { get; private set; } = SavingsAccountStatus.Pending;

    /// <summary>Snapshot of the annual interest rate applicable to this savings contract.</summary>
    public decimal InterestRateSnapshot { get; private set; }

    /// <summary>Snapshot of the governing interest policy version.</summary>
    public int InterestPolicyVersionSnapshot { get; private set; }

    /// <summary>Fixed early withdrawal principal penalty rate (2.5% = 0.025m for FixedLock).</summary>
    public decimal PenaltyRateSnapshot { get; private set; } = 0.025m;

    /// <summary>Target amount for goal-based savings.</summary>
    public decimal? TargetAmount { get; private set; }

    /// <summary>Scheduled recurring contribution amount for goal-based plans.</summary>
    public decimal? ContributionAmount { get; private set; }

    /// <summary>Scheduled recurring contribution frequency.</summary>
    public SavingsContributionFrequency? ContributionFrequency { get; private set; }

    /// <summary>Start timestamp of the savings contract.</summary>
    public DateTime StartDateUtc { get; private set; }

    /// <summary>Maturity timestamp of the savings contract.</summary>
    public DateTime MaturityDateUtc { get; private set; }

    /// <summary>Timestamp when account was matured.</summary>
    public DateTime? MaturedAtUtc { get; private set; }

    /// <summary>Timestamp when account was liquidated/withdrawn.</summary>
    public DateTime? WithdrawnAtUtc { get; private set; }

    /// <summary>Ledger transaction ID corresponding to the final withdrawal settlement.</summary>
    public Guid? WithdrawalLedgerTransactionId { get; private set; }

    /// <summary>Principal penalty assessed upon early withdrawal.</summary>
    public decimal EarlyWithdrawalPenaltyAmount { get; private set; }

    /// <summary>Accrued interest forfeited upon early withdrawal.</summary>
    public decimal ForfeitedInterestAmount { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last state update timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>List of financial contribution records.</summary>
    public IReadOnlyCollection<SavingsContribution> Contributions => _contributions.AsReadOnly();

    /// <summary>List of daily interest accrual records.</summary>
    public IReadOnlyCollection<SavingsInterestAccrual> InterestAccruals => _interestAccruals.AsReadOnly();

    private SavingsAccount() { } // EF Core

    /// <summary>
    /// Opens a new Fixed-Lock savings account instance.
    /// </summary>
    public static SavingsAccount CreateFixedLockAccount(
        Guid savingsPlanId,
        string ownerUserId,
        Guid? organizationId,
        Currency currency,
        decimal interestRate,
        int interestPolicyVersion,
        int durationDays,
        DateTime startDateUtc)
    {
        if (savingsPlanId == Guid.Empty)
            throw new ArgumentException("SavingsPlanId is required.", nameof(savingsPlanId));
        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException("OwnerUserId is required.", nameof(ownerUserId));
        if (durationDays < 30 || durationDays > 730)
            throw new ArgumentException("Fixed-lock duration must be between 30 and 730 days.", nameof(durationDays));

        return new SavingsAccount
        {
            Id = Guid.NewGuid(),
            SavingsPlanId = savingsPlanId,
            OwnerUserId = ownerUserId,
            OrganizationId = organizationId,
            Currency = currency,
            PlanType = SavingsPlanType.FixedLock,
            PrincipalBalance = 0m,
            AccruedInterest = 0m,
            Status = SavingsAccountStatus.Pending,
            InterestRateSnapshot = interestRate,
            InterestPolicyVersionSnapshot = interestPolicyVersion,
            PenaltyRateSnapshot = 0.025m,
            StartDateUtc = startDateUtc,
            MaturityDateUtc = startDateUtc.AddDays(durationDays),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Opens a new Goal-Based savings account instance.
    /// </summary>
    public static SavingsAccount CreateGoalBasedAccount(
        Guid savingsPlanId,
        string ownerUserId,
        Guid? organizationId,
        Currency currency,
        decimal targetAmount,
        decimal contributionAmount,
        SavingsContributionFrequency frequency,
        decimal interestRate,
        int interestPolicyVersion,
        DateTime startDateUtc,
        DateTime targetDateUtc)
    {
        if (savingsPlanId == Guid.Empty)
            throw new ArgumentException("SavingsPlanId is required.", nameof(savingsPlanId));
        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException("OwnerUserId is required.", nameof(ownerUserId));
        if (targetAmount <= 0)
            throw new ArgumentException("TargetAmount must be positive.", nameof(targetAmount));
        if (targetDateUtc <= startDateUtc)
            throw new ArgumentException("TargetDate must be after StartDate.", nameof(targetDateUtc));

        return new SavingsAccount
        {
            Id = Guid.NewGuid(),
            SavingsPlanId = savingsPlanId,
            OwnerUserId = ownerUserId,
            OrganizationId = organizationId,
            Currency = currency,
            PlanType = SavingsPlanType.GoalBased,
            PrincipalBalance = 0m,
            AccruedInterest = 0m,
            Status = SavingsAccountStatus.Pending,
            InterestRateSnapshot = interestRate,
            InterestPolicyVersionSnapshot = interestPolicyVersion,
            PenaltyRateSnapshot = 0m,
            TargetAmount = targetAmount,
            ContributionAmount = contributionAmount,
            ContributionFrequency = frequency,
            StartDateUtc = startDateUtc,
            MaturityDateUtc = targetDateUtc,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Records a settled contribution and activates the account if pending.
    /// </summary>
    public SavingsContribution RecordContribution(decimal amount, Guid ledgerTransactionId, string idempotencyKey)
    {
        if (amount <= 0)
            throw new ArgumentException("Contribution amount must be positive.", nameof(amount));
        if (Status != SavingsAccountStatus.Pending && Status != SavingsAccountStatus.Active)
            throw new InvalidOperationException($"Cannot contribute to savings account with status {Status}.");

        PrincipalBalance += amount;
        Status = SavingsAccountStatus.Active;
        UpdatedAtUtc = DateTime.UtcNow;

        var contribution = SavingsContribution.Create(Id, amount, Currency, ledgerTransactionId, idempotencyKey);
        _contributions.Add(contribution);
        return contribution;
    }

    /// <summary>
    /// Accrues daily interest entitlement.
    /// </summary>
    public SavingsInterestAccrual? AccrueDailyInterest(decimal dailyAmount, DateTime accrualDate, Guid? ledgerTransactionId = null)
    {
        if (Status != SavingsAccountStatus.Active)
            return null;
        if (dailyAmount <= 0)
            return null;

        AccruedInterest += dailyAmount;
        UpdatedAtUtc = DateTime.UtcNow;

        var accrual = SavingsInterestAccrual.Create(
            Id,
            accrualDate.Date,
            PrincipalBalance,
            InterestRateSnapshot,
            dailyAmount,
            InterestPolicyVersionSnapshot,
            ledgerTransactionId);
        _interestAccruals.Add(accrual);
        return accrual;
    }

    /// <summary>
    /// Checks whether the account has reached maturity date.
    /// </summary>
    public void CheckMaturity(DateTime asOfUtc)
    {
        if (Status == SavingsAccountStatus.Active && asOfUtc >= MaturityDateUtc)
        {
            Status = SavingsAccountStatus.Matured;
            MaturedAtUtc = asOfUtc;
            UpdatedAtUtc = asOfUtc;
        }
    }

    /// <summary>
    /// Calculates the exact payout, penalty, and forfeited interest breakdown for withdrawal.
    /// </summary>
    public (decimal PayoutAmount, decimal PenaltyAmount, decimal ForfeitedInterest, bool IsEarly) CalculateWithdrawalTerms(DateTime asOfUtc)
    {
        if (PrincipalBalance <= 0)
            throw new InvalidOperationException("Savings account has no principal balance to withdraw.");

        // Check if matured (or goal-based reached maturity)
        bool isMatured = Status == SavingsAccountStatus.Matured || asOfUtc >= MaturityDateUtc;

        if (isMatured || PlanType == SavingsPlanType.GoalBased)
        {
            // Full payout of principal + accrued interest, 0 penalty
            decimal payout = PrincipalBalance + AccruedInterest;
            return (payout, 0m, 0m, false);
        }

        // Early Fixed-Lock Withdrawal: 100% of accrued interest forfeited + 2.5% principal penalty
        decimal penalty = Math.Round(PrincipalBalance * PenaltyRateSnapshot, 2, MidpointRounding.AwayFromZero);
        decimal netPayout = PrincipalBalance - penalty;
        decimal forfeited = AccruedInterest;

        return (netPayout, penalty, forfeited, true);
    }

    /// <summary>
    /// Executes the withdrawal state transition upon successful ledger settlement.
    /// </summary>
    public void ExecuteWithdrawal(
        decimal payoutAmount,
        decimal penaltyAmount,
        decimal forfeitedInterest,
        Guid ledgerTransactionId,
        DateTime asOfUtc)
    {
        if (Status != SavingsAccountStatus.Active && Status != SavingsAccountStatus.Matured)
            throw new InvalidOperationException($"Cannot withdraw from savings account in status {Status}.");

        Status = SavingsAccountStatus.Withdrawn;
        WithdrawnAtUtc = asOfUtc;
        WithdrawalLedgerTransactionId = ledgerTransactionId;
        EarlyWithdrawalPenaltyAmount = penaltyAmount;
        ForfeitedInterestAmount = forfeitedInterest;
        TotalInterestWithdrawn += (Status == SavingsAccountStatus.Matured || asOfUtc >= MaturityDateUtc) ? AccruedInterest : 0m;
        AccruedInterest = 0m;
        PrincipalBalance = 0m;
        UpdatedAtUtc = asOfUtc;
    }
}
