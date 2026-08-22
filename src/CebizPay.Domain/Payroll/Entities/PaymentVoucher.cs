using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payroll.Enums;

namespace CebizPay.Domain.Payroll.Entities;

/// <summary>
/// Domain entity representing a verifiable Payment Voucher generated upon successful payroll line-item execution.
/// Financial values are strictly immutable; authorized users may edit only non-financial metadata.
/// </summary>
public class PaymentVoucher
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Human-facing unique voucher reference code (e.g. PV-202608-ABC12345).</summary>
    public string VoucherReference { get; private set; } = string.Empty;

    /// <summary>Linked payroll batch ID.</summary>
    public Guid PayrollBatchId { get; private set; }

    /// <summary>Linked payroll line item ID.</summary>
    public Guid PayrollItemId { get; private set; }

    /// <summary>Linked central double-entry ledger transaction ID.</summary>
    public Guid LedgerTransactionId { get; private set; }

    /// <summary>Owning organization tenant ID.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Recipient employee Identity User ID.</summary>
    public string EmployeeUserId { get; private set; } = string.Empty;

    /// <summary>Recipient employee display name at voucher issuance.</summary>
    public string EmployeeName { get; private set; } = string.Empty;

    /// <summary>Gross salary earned.</summary>
    public decimal GrossPay { get; private set; }

    /// <summary>Total deductions applied.</summary>
    public decimal Deductions { get; private set; }

    /// <summary>Net disbursed amount.</summary>
    public decimal NetPay { get; private set; }

    /// <summary>Voucher transactional currency.</summary>
    public Currency Currency { get; private set; }

    /// <summary>Voucher lifecycle status.</summary>
    public VoucherStatus Status { get; private set; } = VoucherStatus.Generated;

    /// <summary>Optional bank / disbursement rail note (metadata editable).</summary>
    public string? BankName { get; private set; }

    /// <summary>Optional authorized administrative remarks (metadata editable).</summary>
    public string? Remarks { get; private set; }

    /// <summary>Optional payment voucher description (metadata editable).</summary>
    public string? Description { get; private set; }

    /// <summary>Voucher issuance timestamp.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last metadata update timestamp.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    private PaymentVoucher() { } // EF Core

    /// <summary>
    /// Generates a new payment voucher linked to a settled payroll item.
    /// </summary>
    public static PaymentVoucher Create(
        Guid payrollBatchId,
        Guid payrollItemId,
        Guid ledgerTransactionId,
        Guid organizationId,
        string employeeUserId,
        string employeeName,
        decimal grossPay,
        decimal deductions,
        decimal netPay,
        Currency currency,
        string? bankName = null,
        string? remarks = null,
        string? description = null,
        string? customReference = null)
    {
        if (payrollBatchId == Guid.Empty)
            throw new ArgumentException("PayrollBatchId is required.", nameof(payrollBatchId));
        if (payrollItemId == Guid.Empty)
            throw new ArgumentException("PayrollItemId is required.", nameof(payrollItemId));
        if (ledgerTransactionId == Guid.Empty)
            throw new ArgumentException("LedgerTransactionId is required.", nameof(ledgerTransactionId));
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(employeeUserId))
            throw new ArgumentException("EmployeeUserId is required.", nameof(employeeUserId));

        currency.EnsureTransactionalV1();

        var refCode = customReference ?? $"PV-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        return new PaymentVoucher
        {
            Id = Guid.NewGuid(),
            VoucherReference = refCode,
            PayrollBatchId = payrollBatchId,
            PayrollItemId = payrollItemId,
            LedgerTransactionId = ledgerTransactionId,
            OrganizationId = organizationId,
            EmployeeUserId = employeeUserId,
            EmployeeName = employeeName.Trim(),
            GrossPay = grossPay,
            Deductions = deductions,
            NetPay = netPay,
            Currency = currency,
            Status = VoucherStatus.Generated,
            BankName = bankName?.Trim(),
            Remarks = remarks?.Trim(),
            Description = description?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Updates safe non-financial voucher metadata (BankName, Remarks, Description).
    /// Financial amounts, currency, and ledger links remain strictly immutable.
    /// </summary>
    public void UpdateMetadata(string? bankName, string? remarks, string? description)
    {
        BankName = bankName?.Trim();
        Remarks = remarks?.Trim();
        Description = description?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the voucher as voided.
    /// </summary>
    public void MarkVoided()
    {
        Status = VoucherStatus.Voided;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
