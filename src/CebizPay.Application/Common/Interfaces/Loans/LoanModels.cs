using CebizPay.Domain.Loans.Enums;

namespace CebizPay.Application.Common.Interfaces.Loans;

/// <summary>Request payload for previewing a loan calculation.</summary>
public sealed record LoanCalculationPreviewRequest(
    Guid LoanPlanId,
    decimal RequestedAmount,
    int DurationMonths);

/// <summary>DTO returned from loan calculation preview.</summary>
public sealed record LoanCalculationPreviewDto(
    decimal RequestedAmount,
    decimal AnnualInterestRate,
    int DurationMonths,
    decimal MonthlyPayment,
    decimal TotalInterest,
    decimal TotalRepayment,
    decimal VerifiedSalary,
    decimal ExistingMonthlyDebt,
    decimal ProposedMonthlyPayment,
    decimal TotalMonthlyDebt,
    decimal DebtToIncomeRatio,
    decimal MaxAllowedMonthlyDebt,
    bool IsDtiCompliant,
    bool IsEligible,
    string? IneligibilityReason);

/// <summary>DTO representing a corporate loan plan.</summary>
public sealed record CorporateLoanPlanDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    decimal MinimumAmount,
    decimal MaximumAmount,
    decimal InterestRate,
    int MinimumDurationMonths,
    int MaximumDurationMonths,
    RepaymentFrequency RepaymentFrequency,
    decimal MinimumMonthlySalary,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>Request payload for creating a corporate loan plan.</summary>
public sealed record CreateLoanPlanRequest(
    string Name,
    string? Description,
    decimal MinimumAmount,
    decimal MaximumAmount,
    decimal InterestRate,
    int MinimumDurationMonths,
    int MaximumDurationMonths,
    decimal MinimumMonthlySalary,
    RepaymentFrequency RepaymentFrequency = RepaymentFrequency.Monthly);

/// <summary>Request payload for updating an existing corporate loan plan.</summary>
public sealed record UpdateLoanPlanRequest(
    string Name,
    string? Description,
    decimal MinimumAmount,
    decimal MaximumAmount,
    decimal InterestRate,
    int MinimumDurationMonths,
    int MaximumDurationMonths,
    decimal MinimumMonthlySalary,
    bool IsActive,
    RepaymentFrequency RepaymentFrequency = RepaymentFrequency.Monthly);

/// <summary>Request payload for submitting a staff loan application.</summary>
public sealed record SubmitLoanApplicationRequest(
    Guid LoanPlanId,
    decimal RequestedAmount,
    int DurationMonths,
    RepaymentFrequency RepaymentFrequency = RepaymentFrequency.Monthly);

/// <summary>DTO representing a staff loan application.</summary>
public sealed record LoanApplicationDto(
    Guid Id,
    string ApplicationReference,
    Guid OrganizationId,
    Guid LoanPlanId,
    string ApplicantUserId,
    string ApplicantName,
    decimal RequestedAmount,
    decimal InterestRateSnapshot,
    int DurationMonths,
    RepaymentFrequency RepaymentFrequency,
    decimal ComputedMonthlyPayment,
    decimal ComputedTotalInterest,
    decimal ComputedTotalRepayment,
    decimal VerifiedSalarySnapshot,
    decimal ExistingMonthlyDebtSnapshot,
    decimal ProposedMonthlyPaymentSnapshot,
    decimal TotalMonthlyDebtSnapshot,
    decimal DebtToIncomeRatioSnapshot,
    bool IsDtiCompliantSnapshot,
    LoanApplicationStatus Status,
    string? UnderwritingReason,
    string? DeclinedReason,
    string? DeciderUserId,
    DateTime CreatedAtUtc,
    DateTime? DecidedAtUtc);

/// <summary>Request payload for declining a loan application.</summary>
public sealed record DeclineLoanApplicationRequest(string Reason);

/// <summary>DTO representing a loan contract and its repayment schedule.</summary>
public sealed record LoanContractDto(
    Guid Id,
    string ContractReference,
    Guid? LoanApplicationId,
    Guid OrganizationId,
    string BorrowerUserId,
    string BorrowerName,
    LoanType LoanType,
    decimal OriginalPrincipal,
    decimal InterestRate,
    decimal TotalInterest,
    decimal TotalRepayment,
    RepaymentFrequency RepaymentFrequency,
    int NumberOfInstallments,
    decimal MonthlyInstallmentAmount,
    decimal OutstandingPrincipal,
    decimal TotalAmountPaid,
    DateTime StartDate,
    DateTime ExpectedEndDate,
    LoanContractStatus Status,
    Guid? DisbursementLedgerTransactionId,
    DateTime? DisbursedAtUtc,
    Guid? ConvertedToContractId,
    Guid? ConvertedFromContractId,
    DateTime? ConvertedAtUtc,
    string? ConversionReason,
    DateTime CreatedAtUtc,
    IReadOnlyList<LoanRepaymentScheduleItemDto> RepaymentSchedule);

/// <summary>DTO representing a single repayment installment.</summary>
public sealed record LoanRepaymentScheduleItemDto(
    Guid Id,
    Guid LoanContractId,
    int InstallmentNumber,
    DateTime DueDate,
    decimal ScheduledAmount,
    decimal PrincipalComponent,
    decimal InterestComponent,
    decimal PaidAmount,
    LoanRepaymentStatus Status,
    DateTime? PaidAtUtc,
    DateTime? MissedAtUtc,
    Guid? PayrollItemId,
    Guid? LedgerTransactionId);

/// <summary>Request payload for converting outstanding staff loans upon offboarding.</summary>
public sealed record ConvertStaffLoansRequest(string Reason);

/// <summary>Structured result from deterministic underwriting evaluation.</summary>
public sealed record UnderwritingEvaluationResult(
    bool Eligible,
    decimal VerifiedSalary,
    decimal ExistingMonthlyDebt,
    decimal ProposedMonthlyPayment,
    decimal TotalMonthlyDebt,
    decimal MaxAllowedDebt,
    decimal DebtToIncomeRatio,
    bool IsDtiCompliant,
    string? Reason);
