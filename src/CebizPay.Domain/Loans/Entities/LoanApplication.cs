using CebizPay.Domain.Loans.Enums;

namespace CebizPay.Domain.Loans.Entities;

/// <summary>
/// Domain aggregate root representing a corporate staff loan application.
/// Snapshots underwriting calculations, 33% DTI ratio evaluations, and enforces strict state transitions and self-approval prevention.
/// </summary>
public class LoanApplication
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Unique human-facing application tracking reference (e.g. LA-202608-ABC12345).</summary>
    public string ApplicationReference { get; private set; } = string.Empty;

    /// <summary>Owning organization tenant ID.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Corporate loan plan ID applied under.</summary>
    public Guid LoanPlanId { get; private set; }

    /// <summary>Applicant employee Identity User ID.</summary>
    public string ApplicantUserId { get; private set; } = string.Empty;

    /// <summary>Applicant employee display name at submission time.</summary>
    public string ApplicantName { get; private set; } = string.Empty;

    /// <summary>Requested loan principal amount.</summary>
    public decimal RequestedAmount { get; private set; }

    /// <summary>Snapshot of annual interest rate at application time.</summary>
    public decimal InterestRateSnapshot { get; private set; }

    /// <summary>Requested duration in months.</summary>
    public int DurationMonths { get; private set; }

    /// <summary>Repayment frequency (default Monthly).</summary>
    public RepaymentFrequency RepaymentFrequency { get; private set; } = RepaymentFrequency.Monthly;

    /// <summary>Computed flat monthly repayment installment.</summary>
    public decimal ComputedMonthlyPayment { get; private set; }

    /// <summary>Computed total flat interest over the loan lifespan.</summary>
    public decimal ComputedTotalInterest { get; private set; }

    /// <summary>Computed total repayment amount (Principal + Total Interest).</summary>
    public decimal ComputedTotalRepayment { get; private set; }

    /// <summary>Snapshot of verified monthly base salary used for underwriting.</summary>
    public decimal VerifiedSalarySnapshot { get; private set; }

    /// <summary>Snapshot of existing monthly debt commitments across all active loans.</summary>
    public decimal ExistingMonthlyDebtSnapshot { get; private set; }

    /// <summary>Snapshot of proposed new loan monthly installment.</summary>
    public decimal ProposedMonthlyPaymentSnapshot { get; private set; }

    /// <summary>Snapshot of aggregated monthly debt obligations (Existing + Proposed).</summary>
    public decimal TotalMonthlyDebtSnapshot { get; private set; }

    /// <summary>Snapshot of computed Debt-To-Income percentage ratio (0.00m to 1.00m).</summary>
    public decimal DebtToIncomeRatioSnapshot { get; private set; }

    /// <summary>Indicates whether total debt obligations satisfy the non-negotiable 33% DTI ceiling.</summary>
    public bool IsDtiCompliantSnapshot { get; private set; }

    /// <summary>Current lifecycle status of the application.</summary>
    public LoanApplicationStatus Status { get; private set; } = LoanApplicationStatus.Draft;

    /// <summary>Underwriting outcome rationale or manual review note.</summary>
    public string? UnderwritingReason { get; private set; }

    /// <summary>Explicit reason if declined.</summary>
    public string? DeclinedReason { get; private set; }

    /// <summary>Identity user ID of the authorized approver / reviewer / decider.</summary>
    public string? DeciderUserId { get; private set; }

    /// <summary>Application creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last application state update timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Timestamp when final approval or decline was recorded.</summary>
    public DateTime? DecidedAtUtc { get; private set; }

    private LoanApplication() { } // EF Core

    /// <summary>
    /// Creates a new loan application in Draft or Submitted status with snapshotted underwriting metrics.
    /// </summary>
    public static LoanApplication Create(
        Guid organizationId,
        Guid loanPlanId,
        string applicantUserId,
        string applicantName,
        decimal requestedAmount,
        decimal interestRateSnapshot,
        int durationMonths,
        decimal computedMonthlyPayment,
        decimal computedTotalInterest,
        decimal computedTotalRepayment,
        decimal verifiedSalarySnapshot,
        decimal existingMonthlyDebtSnapshot,
        decimal proposedMonthlyPaymentSnapshot,
        decimal totalMonthlyDebtSnapshot,
        decimal debtToIncomeRatioSnapshot,
        bool isDtiCompliantSnapshot,
        string? underwritingReason = null,
        RepaymentFrequency repaymentFrequency = RepaymentFrequency.Monthly,
        bool autoSubmit = true)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));
        if (loanPlanId == Guid.Empty)
            throw new ArgumentException("LoanPlanId is required.", nameof(loanPlanId));
        if (string.IsNullOrWhiteSpace(applicantUserId))
            throw new ArgumentException("ApplicantUserId is required.", nameof(applicantUserId));
        if (requestedAmount <= 0)
            throw new ArgumentException("RequestedAmount must be positive.", nameof(requestedAmount));
        if (durationMonths <= 0)
            throw new ArgumentException("DurationMonths must be positive.", nameof(durationMonths));

        var refCode = $"LA-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var status = autoSubmit
            ? (isDtiCompliantSnapshot && verifiedSalarySnapshot > 0 ? LoanApplicationStatus.Submitted : LoanApplicationStatus.UnderReview)
            : LoanApplicationStatus.Draft;

        return new LoanApplication
        {
            Id = Guid.NewGuid(),
            ApplicationReference = refCode,
            OrganizationId = organizationId,
            LoanPlanId = loanPlanId,
            ApplicantUserId = applicantUserId,
            ApplicantName = applicantName.Trim(),
            RequestedAmount = requestedAmount,
            InterestRateSnapshot = interestRateSnapshot,
            DurationMonths = durationMonths,
            RepaymentFrequency = repaymentFrequency,
            ComputedMonthlyPayment = computedMonthlyPayment,
            ComputedTotalInterest = computedTotalInterest,
            ComputedTotalRepayment = computedTotalRepayment,
            VerifiedSalarySnapshot = verifiedSalarySnapshot,
            ExistingMonthlyDebtSnapshot = existingMonthlyDebtSnapshot,
            ProposedMonthlyPaymentSnapshot = proposedMonthlyPaymentSnapshot,
            TotalMonthlyDebtSnapshot = totalMonthlyDebtSnapshot,
            DebtToIncomeRatioSnapshot = debtToIncomeRatioSnapshot,
            IsDtiCompliantSnapshot = isDtiCompliantSnapshot,
            UnderwritingReason = underwritingReason,
            Status = status,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Submits a draft application for review.
    /// </summary>
    public void Submit()
    {
        if (Status != LoanApplicationStatus.Draft)
            throw new InvalidOperationException($"Cannot submit application with status {Status}.");

        Status = IsDtiCompliantSnapshot && VerifiedSalarySnapshot > 0
            ? LoanApplicationStatus.Submitted
            : LoanApplicationStatus.UnderReview;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Places application under manual review.
    /// </summary>
    public void PutUnderReview(string reviewerId, string? reason)
    {
        if (Status != LoanApplicationStatus.Submitted && Status != LoanApplicationStatus.Draft)
            throw new InvalidOperationException($"Cannot put application with status {Status} under review.");

        Status = LoanApplicationStatus.UnderReview;
        DeciderUserId = reviewerId;
        UnderwritingReason = reason;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Formally approves the loan application, preventing self-approval.
    /// </summary>
    public void Approve(string approverUserId)
    {
        if (string.IsNullOrWhiteSpace(approverUserId))
            throw new ArgumentException("ApproverUserId is required.", nameof(approverUserId));

        if (string.Equals(approverUserId, ApplicantUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Self-approval is strictly forbidden. A staff member cannot approve their own loan application.");
        }

        if (Status == LoanApplicationStatus.Approved)
            return; // Idempotent

        if (Status != LoanApplicationStatus.Submitted && Status != LoanApplicationStatus.UnderReview)
        {
            throw new InvalidOperationException($"Cannot approve application with status {Status}.");
        }

        Status = LoanApplicationStatus.Approved;
        DeciderUserId = approverUserId;
        DecidedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Formally declines the loan application.
    /// </summary>
    public void Decline(string deciderUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(deciderUserId))
            throw new ArgumentException("DeciderUserId is required.", nameof(deciderUserId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Declined reason is required.", nameof(reason));

        if (Status == LoanApplicationStatus.Declined)
            return; // Idempotent

        if (Status == LoanApplicationStatus.Approved)
            throw new InvalidOperationException("Cannot decline an already approved loan application.");

        Status = LoanApplicationStatus.Declined;
        DeciderUserId = deciderUserId;
        DeclinedReason = reason.Trim();
        DecidedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Cancels the loan application before decision.
    /// </summary>
    public void Cancel(string userId)
    {
        if (Status == LoanApplicationStatus.Approved || Status == LoanApplicationStatus.Declined)
            throw new InvalidOperationException($"Cannot cancel decided loan application with status {Status}.");

        Status = LoanApplicationStatus.Cancelled;
        DeciderUserId = userId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
