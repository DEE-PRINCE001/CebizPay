using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payroll.Enums;

namespace CebizPay.Domain.Payroll.Events;

/// <summary>Domain event published when a payroll batch is created and scheduled.</summary>
public sealed record PayrollBatchCreatedDomainEvent(
    Guid PayrollBatchId,
    string BatchReference,
    Guid OrganizationId,
    Currency Currency,
    int TotalEmployees,
    decimal TotalNetAmount,
    DateTime OccurredOnUtc);

/// <summary>Domain event published when worker begins processing a payroll batch.</summary>
public sealed record PayrollBatchStartedDomainEvent(
    Guid PayrollBatchId,
    string BatchReference,
    Guid OrganizationId,
    DateTime OccurredOnUtc);

/// <summary>Domain event published when an individual payroll item is financially settled.</summary>
public sealed record PayrollItemCompletedDomainEvent(
    Guid PayrollBatchId,
    Guid PayrollItemId,
    Guid OrganizationId,
    string EmployeeUserId,
    decimal NetPay,
    Currency Currency,
    Guid LedgerTransactionId,
    Guid PaymentVoucherId,
    DateTime OccurredOnUtc);

/// <summary>Domain event published when an individual payroll item financial execution fails.</summary>
public sealed record PayrollItemFailedDomainEvent(
    Guid PayrollBatchId,
    Guid PayrollItemId,
    Guid OrganizationId,
    string EmployeeUserId,
    string FailureCode,
    string FailureReason,
    int AttemptNumber,
    DateTime OccurredOnUtc);

/// <summary>Domain event published when a failed payroll item is retried.</summary>
public sealed record PayrollItemRetriedDomainEvent(
    Guid PayrollBatchId,
    Guid PayrollItemId,
    Guid OrganizationId,
    DateTime OccurredOnUtc);

/// <summary>Domain event published when a payroll batch fully completes.</summary>
public sealed record PayrollBatchCompletedDomainEvent(
    Guid PayrollBatchId,
    string BatchReference,
    Guid OrganizationId,
    int TotalCompleted,
    decimal TotalDisbursed,
    Currency Currency,
    DateTime OccurredOnUtc);

/// <summary>Domain event published when a payroll batch concludes with some items failed.</summary>
public sealed record PayrollBatchPartiallyCompletedDomainEvent(
    Guid PayrollBatchId,
    string BatchReference,
    Guid OrganizationId,
    int CompletedCount,
    int FailedCount,
    Currency Currency,
    DateTime OccurredOnUtc);

/// <summary>Domain event published when a payroll batch completely fails.</summary>
public sealed record PayrollBatchFailedDomainEvent(
    Guid PayrollBatchId,
    string BatchReference,
    Guid OrganizationId,
    string FailureReason,
    DateTime OccurredOnUtc);

/// <summary>Domain event published when a payment voucher is issued.</summary>
public sealed record PaymentVoucherCreatedDomainEvent(
    Guid PaymentVoucherId,
    string VoucherReference,
    Guid PayrollBatchId,
    Guid PayrollItemId,
    Guid OrganizationId,
    string EmployeeUserId,
    decimal NetPay,
    Currency Currency,
    DateTime OccurredOnUtc);

/// <summary>Domain event published when non-financial payment voucher metadata is modified.</summary>
public sealed record PaymentVoucherMetadataUpdatedDomainEvent(
    Guid PaymentVoucherId,
    string VoucherReference,
    Guid OrganizationId,
    string? BankName,
    string? Remarks,
    DateTime OccurredOnUtc);
