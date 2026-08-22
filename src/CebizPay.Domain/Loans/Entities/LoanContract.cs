using CebizPay.Domain.Loans.Enums;

namespace CebizPay.Domain.Loans.Entities;

/// <summary>
/// Domain aggregate root representing an active or completed legal loan contract obligation.
/// Encapsulates principal/interest tracking, disbursement linkage, repayment schedules, and offboarding conversion state.
/// </summary>
public class LoanContract
{
    private readonly List<LoanRepaymentScheduleItem> _repaymentSchedule = new();

    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Unique contract reference code (e.g. LC-202608-ABC12345).</summary>
    public string ContractReference { get; private set; } = string.Empty;

    /// <summary>Originating loan application ID (if initiated via corporate staff application).</summary>
    public Guid? LoanApplicationId { get; private set; }

    /// <summary>Owning organization tenant ID.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Borrower employee / individual Identity User ID.</summary>
    public string BorrowerUserId { get; private set; } = string.Empty;

    /// <summary>Borrower display name at contract issuance.</summary>
    public string BorrowerName { get; private set; } = string.Empty;

    /// <summary>Classification of loan obligation (CorporatePayrollLoan vs StandardIndividualLoan).</summary>
    public LoanType LoanType { get; private set; } = LoanType.CorporatePayrollLoan;

    /// <summary>Original borrowed principal amount.</summary>
    public decimal OriginalPrincipal { get; private set; }

    /// <summary>Annual flat interest rate.</summary>
    public decimal InterestRate { get; private set; }

    /// <summary>Total flat interest computed over the duration.</summary>
    public decimal TotalInterest { get; private set; }

    /// <summary>Total contract repayment obligation (Principal + Total Interest).</summary>
    public decimal TotalRepayment { get; private set; }

    /// <summary>Repayment frequency (default Monthly).</summary>
    public RepaymentFrequency RepaymentFrequency { get; private set; } = RepaymentFrequency.Monthly;

    /// <summary>Total number of scheduled repayment installments.</summary>
    public int NumberOfInstallments { get; private set; }

    /// <summary>Standard monthly installment amount.</summary>
    public decimal MonthlyInstallmentAmount { get; private set; }

    /// <summary>Current outstanding principal balance remaining.</summary>
    public decimal OutstandingPrincipal { get; private set; }

    /// <summary>Total cumulative amount paid to date.</summary>
    public decimal TotalAmountPaid { get; private set; }

    /// <summary>Contract commencement date.</summary>
    public DateTime StartDate { get; private set; }

    /// <summary>Expected maturity / final installment date.</summary>
    public DateTime ExpectedEndDate { get; private set; }

    /// <summary>Contract lifecycle status.</summary>
    public LoanContractStatus Status { get; private set; } = LoanContractStatus.Active;

    /// <summary>Central double-entry ledger transaction ID recording loan principal disbursement.</summary>
    public Guid? DisbursementLedgerTransactionId { get; private set; }

    /// <summary>Timestamp when disbursement occurred.</summary>
    public DateTime? DisbursedAtUtc { get; private set; }

    /// <summary>Linked target individual loan contract ID if converted upon staff offboarding.</summary>
    public Guid? ConvertedToContractId { get; private set; }

    /// <summary>Linked source payroll loan contract ID if this contract was created via offboarding conversion.</summary>
    public Guid? ConvertedFromContractId { get; private set; }

    /// <summary>Timestamp when conversion occurred.</summary>
    public DateTime? ConvertedAtUtc { get; private set; }

    /// <summary>Reason for offboarding conversion.</summary>
    public string? ConversionReason { get; private set; }

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last contract update timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Ordered list of repayment schedule installment items.</summary>
    public IReadOnlyCollection<LoanRepaymentScheduleItem> RepaymentSchedule => _repaymentSchedule.AsReadOnly();

    private LoanContract() { } // EF Core

    /// <summary>
    /// Generates a new active corporate loan contract from an approved loan application.
    /// </summary>
    public static LoanContract CreateFromApplication(LoanApplication application, string? customReference = null)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (application.Status != LoanApplicationStatus.Approved)
            throw new InvalidOperationException("Contract can only be created from an APPROVED loan application.");

        var refCode = customReference ?? $"LC-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var startDate = DateTime.UtcNow;
        var endDate = startDate.AddMonths(application.DurationMonths);

        return new LoanContract
        {
            Id = Guid.NewGuid(),
            ContractReference = refCode,
            LoanApplicationId = application.Id,
            OrganizationId = application.OrganizationId,
            BorrowerUserId = application.ApplicantUserId,
            BorrowerName = application.ApplicantName,
            LoanType = LoanType.CorporatePayrollLoan,
            OriginalPrincipal = application.RequestedAmount,
            InterestRate = application.InterestRateSnapshot,
            TotalInterest = application.ComputedTotalInterest,
            TotalRepayment = application.ComputedTotalRepayment,
            RepaymentFrequency = application.RepaymentFrequency,
            NumberOfInstallments = application.DurationMonths,
            MonthlyInstallmentAmount = application.ComputedMonthlyPayment,
            OutstandingPrincipal = application.RequestedAmount,
            TotalAmountPaid = 0m,
            StartDate = startDate,
            ExpectedEndDate = endDate,
            Status = LoanContractStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Generates a standard individual loan contract converted from an outstanding payroll loan upon staff offboarding.
    /// </summary>
    public static LoanContract CreateConvertedIndividualLoan(
        LoanContract originalPayrollLoan,
        string conversionReason,
        string? customReference = null)
    {
        ArgumentNullException.ThrowIfNull(originalPayrollLoan);

        var remainingTotalDebt = originalPayrollLoan.TotalRepayment - originalPayrollLoan.TotalAmountPaid;
        var remainingPrincipal = Math.Min(originalPayrollLoan.OutstandingPrincipal, remainingTotalDebt);

        var unpaidItems = originalPayrollLoan.RepaymentSchedule
            .Where(i => i.Status != LoanRepaymentStatus.Paid && i.Status != LoanRepaymentStatus.Waived)
            .OrderBy(i => i.InstallmentNumber)
            .ToList();

        var remainingInstallments = unpaidItems.Count > 0 ? unpaidItems.Count : 1;
        var installmentAmount = remainingTotalDebt / remainingInstallments;

        var refCode = customReference ?? $"LCI-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var startDate = DateTime.UtcNow;
        var endDate = startDate.AddMonths(remainingInstallments);

        var newContract = new LoanContract
        {
            Id = Guid.NewGuid(),
            ContractReference = refCode,
            LoanApplicationId = null,
            OrganizationId = originalPayrollLoan.OrganizationId,
            BorrowerUserId = originalPayrollLoan.BorrowerUserId,
            BorrowerName = originalPayrollLoan.BorrowerName,
            LoanType = LoanType.StandardIndividualLoan,
            OriginalPrincipal = remainingPrincipal,
            InterestRate = originalPayrollLoan.InterestRate,
            TotalInterest = remainingTotalDebt - remainingPrincipal,
            TotalRepayment = remainingTotalDebt,
            RepaymentFrequency = originalPayrollLoan.RepaymentFrequency,
            NumberOfInstallments = remainingInstallments,
            MonthlyInstallmentAmount = installmentAmount,
            OutstandingPrincipal = remainingPrincipal,
            TotalAmountPaid = 0m,
            StartDate = startDate,
            ExpectedEndDate = endDate,
            Status = LoanContractStatus.Active,
            ConvertedFromContractId = originalPayrollLoan.Id,
            ConversionReason = conversionReason.Trim(),
            ConvertedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };

        return newContract;
    }

    /// <summary>
    /// Attaches an installment item to the repayment schedule.
    /// </summary>
    public void AddScheduleItem(LoanRepaymentScheduleItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _repaymentSchedule.Add(item);
    }

    /// <summary>
    /// Marks the loan principal as disbursed to the borrower wallet.
    /// </summary>
    public void MarkDisbursed(Guid ledgerTransactionId)
    {
        if (ledgerTransactionId == Guid.Empty)
            throw new ArgumentException("LedgerTransactionId is required.", nameof(ledgerTransactionId));

        DisbursementLedgerTransactionId = ledgerTransactionId;
        DisbursedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies a repayment installment and reduces remaining balances.
    /// </summary>
    public void ApplyRepayment(int installmentNumber, decimal amount, Guid? payrollItemId = null, Guid? ledgerTxnId = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Payment amount must be positive.", nameof(amount));

        var item = _repaymentSchedule.Find(i => i.InstallmentNumber == installmentNumber);
        if (item == null)
        {
            throw new InvalidOperationException($"Installment #{installmentNumber} not found on contract '{ContractReference}'.");
        }

        item.MarkPaid(amount, payrollItemId, ledgerTxnId);
        TotalAmountPaid += amount;

        // Reduce outstanding principal (proportional or matching principal component)
        var principalPaid = Math.Min(item.PrincipalComponent, OutstandingPrincipal);
        OutstandingPrincipal = Math.Max(0m, OutstandingPrincipal - principalPaid);

        if (TotalAmountPaid >= TotalRepayment || _repaymentSchedule.All(i => i.Status == LoanRepaymentStatus.Paid || i.Status == LoanRepaymentStatus.Waived))
        {
            Status = LoanContractStatus.PaidOff;
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Evaluates whether any overdue installments transition contract to Overdue status.
    /// </summary>
    public void CheckOverdue(DateTime asOfUtc)
    {
        if (Status == LoanContractStatus.PaidOff || Status == LoanContractStatus.ConvertedToIndividual || Status == LoanContractStatus.Cancelled)
            return;

        var hasOverdue = _repaymentSchedule.Any(i => i.DueDate < asOfUtc && i.Status != LoanRepaymentStatus.Paid && i.Status != LoanRepaymentStatus.Waived);
        if (hasOverdue)
        {
            Status = LoanContractStatus.Overdue;
            foreach (var item in _repaymentSchedule.Where(i => i.DueDate < asOfUtc && i.Status == LoanRepaymentStatus.Pending))
            {
                item.MarkMissed();
            }
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Marks the corporate payroll loan as converted to an individual loan upon staff offboarding.
    /// </summary>
    public void ConvertToIndividual(Guid newContractId, string reason)
    {
        if (newContractId == Guid.Empty)
            throw new ArgumentException("NewContractId is required.", nameof(newContractId));

        Status = LoanContractStatus.ConvertedToIndividual;
        ConvertedToContractId = newContractId;
        ConversionReason = reason.Trim();
        ConvertedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
