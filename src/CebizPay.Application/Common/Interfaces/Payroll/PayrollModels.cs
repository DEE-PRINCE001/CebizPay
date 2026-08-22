using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payroll.Enums;

namespace CebizPay.Application.Common.Interfaces.Payroll;

/// <summary>
/// Criteria for selecting employees eligible for a payroll calculation or execution.
/// </summary>
public sealed record PayrollSelectionCriteria(
    PayrollSelectionMode Mode = PayrollSelectionMode.All,
    IReadOnlyList<Guid>? DepartmentIds = null,
    IReadOnlyList<Guid>? WorkforceRoleIds = null,
    IReadOnlyList<Guid>? SalaryLevelIds = null,
    IReadOnlyList<string>? EmployeeUserIds = null);

/// <summary>
/// Line-item calculation summary for a single employee.
/// </summary>
public sealed record PayrollCalculationItemDto(
    string EmployeeUserId,
    string EmployeeName,
    string EmployeeEmail,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? WorkforceRoleId,
    string? RoleTitle,
    Guid? SalaryLevelId,
    string? SalaryLevelName,
    decimal GrossPay,
    decimal TotalDeductions,
    decimal NetPay,
    Currency Currency,
    IReadOnlyList<PayrollDeductionDetailDto>? Deductions);

/// <summary>
/// Breakdown of an individual deduction applied to an employee.
/// </summary>
public sealed record PayrollDeductionDetailDto(
    string DeductionType,
    decimal Amount,
    string? Reference,
    string? Description);

/// <summary>
/// Overall preview result from a payroll calculation run.
/// </summary>
public sealed record PayrollCalculationResultDto(
    Guid OrganizationId,
    Currency Currency,
    int TotalEmployees,
    decimal TotalGrossAmount,
    decimal TotalDeductionsAmount,
    decimal TotalNetAmount,
    IReadOnlyList<PayrollCalculationItemDto> Items);

/// <summary>
/// High-level response DTO representing a newly scheduled payroll batch.
/// </summary>
public sealed record PayrollBatchDto(
    Guid BatchId,
    string BatchReference,
    Guid OrganizationId,
    Currency Currency,
    PayrollBatchStatus Status,
    int TotalEmployees,
    decimal TotalGrossAmount,
    decimal TotalDeductionsAmount,
    decimal TotalNetAmount,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime CreatedAtUtc);

/// <summary>
/// Progress reporting model for an in-flight or completed payroll batch.
/// </summary>
public sealed record PayrollBatchProgressDto(
    Guid BatchId,
    string BatchReference,
    Guid OrganizationId,
    Currency Currency,
    PayrollBatchStatus Status,
    int TotalEmployees,
    int CompletedCount,
    int ProcessingCount,
    int PendingCount,
    int FailedCount,
    int RetryPendingCount,
    decimal ProgressPercentage,
    decimal TotalGrossAmount,
    decimal TotalDeductionsAmount,
    decimal TotalNetAmount,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? FailureReason,
    IReadOnlyList<PayrollItemProgressDto> Items);

/// <summary>
/// Detailed state of an individual payroll item for progress tracking.
/// </summary>
public sealed record PayrollItemProgressDto(
    Guid ItemId,
    string EmployeeUserId,
    string EmployeeName,
    string EmployeeEmail,
    decimal GrossPay,
    decimal TotalDeductions,
    decimal NetPay,
    Currency Currency,
    PayrollItemStatus Status,
    int CurrentAttemptNumber,
    string? LastFailureCode,
    string? LastFailureReason,
    Guid? PaymentVoucherId,
    Guid? LedgerTransactionId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// Payment Voucher response model.
/// </summary>
public sealed record PaymentVoucherDto(
    Guid Id,
    string VoucherReference,
    Guid PayrollBatchId,
    Guid PayrollItemId,
    Guid LedgerTransactionId,
    Guid OrganizationId,
    string EmployeeUserId,
    string EmployeeName,
    decimal GrossPay,
    decimal Deductions,
    decimal NetPay,
    Currency Currency,
    VoucherStatus Status,
    string? BankName,
    string? Remarks,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// Request model for editing safe non-financial voucher metadata.
/// </summary>
public sealed record UpdatePaymentVoucherMetadataRequest(
    string? BankName,
    string? Remarks,
    string? Description);

/// <summary>
/// Result of an individual payroll item execution attempt.
/// </summary>
public sealed record PayrollItemExecutionResult(
    bool Succeeded,
    Guid? LedgerTransactionId,
    Guid? PaymentVoucherId,
    string? FailureCode,
    string? FailureReason);

/// <summary>
/// Read-only aggregated analytics for administrative oversight.
/// </summary>
public sealed record PayrollAnalyticsDto(
    Guid OrganizationId,
    int TotalBatchesCount,
    int TotalDisbursedItemsCount,
    decimal TotalDisbursedNgn,
    decimal TotalDisbursedInternationalNgn,
    decimal TotalDisbursedUsdt,
    DateTime? LastPayrollExecutedAtUtc);
