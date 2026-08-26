#pragma warning disable CS1591
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Erp.Entities;

/// <summary>
/// Domain aggregate representing an ERP company disbursement voucher.
/// Distinct from payroll PaymentVoucher.
/// </summary>
public sealed class CompanyVoucher
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string VoucherNumber { get; private set; } = string.Empty;
    public string PayeeName { get; private set; } = string.Empty;
    public string? PayeeDetails { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public Currency Currency { get; private set; }
    public CompanyVoucherPaymentMethod PaymentMethod { get; private set; }
    public CompanyVoucherStatus Status { get; private set; }
    public string CreatedByUserId { get; private set; } = string.Empty;
    public string? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public Guid? WalletId { get; private set; }
    public Guid? LedgerTransactionId { get; private set; }
    public string? Reference { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private CompanyVoucher() { } // EF Core

    public CompanyVoucher(
        Guid organizationId,
        string voucherNumber,
        string payeeName,
        string purpose,
        decimal amount,
        string createdByUserId,
        Currency currency = Currency.NGN,
        CompanyVoucherPaymentMethod paymentMethod = CompanyVoucherPaymentMethod.Manual,
        string? payeeDetails = null,
        string? notes = null,
        string? reference = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(voucherNumber))
        {
            throw new ArgumentException("VoucherNumber is required.", nameof(voucherNumber));
        }

        if (string.IsNullOrWhiteSpace(payeeName))
        {
            throw new ArgumentException("PayeeName is required.", nameof(payeeName));
        }

        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new ArgumentException("Purpose is required.", nameof(purpose));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Voucher amount must be positive.");
        }

        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new ArgumentException("CreatedByUserId cannot be empty.", nameof(createdByUserId));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        VoucherNumber = voucherNumber.Trim().ToUpperInvariant();
        PayeeName = payeeName.Trim();
        Purpose = purpose.Trim();
        Amount = amount;
        Currency = currency;
        PaymentMethod = paymentMethod;
        CreatedByUserId = createdByUserId.Trim();
        PayeeDetails = string.IsNullOrWhiteSpace(payeeDetails) ? null : payeeDetails.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        Status = CompanyVoucherStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Approves the company voucher for payment/disbursement.
    /// </summary>
    public void Approve(string approvedByUserId, DateTime utcNow)
    {
        if (Status != CompanyVoucherStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot approve voucher in status '{Status}'. Only Draft vouchers can be approved.");
        }

        if (string.IsNullOrWhiteSpace(approvedByUserId))
        {
            throw new ArgumentException("ApprovedByUserId cannot be empty.", nameof(approvedByUserId));
        }

        Status = CompanyVoucherStatus.Approved;
        ApprovedByUserId = approvedByUserId.Trim();
        ApprovedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Marks the approved company voucher as Paid.
    /// </summary>
    public void MarkPaid(DateTime utcNow, Guid? walletId = null, Guid? ledgerTransactionId = null, string? reference = null)
    {
        if (Status != CompanyVoucherStatus.Approved)
        {
            throw new InvalidOperationException($"Cannot pay voucher in status '{Status}'. It must be Approved first.");
        }

        Status = CompanyVoucherStatus.Paid;
        PaidAtUtc = utcNow;
        WalletId = walletId;
        LedgerTransactionId = ledgerTransactionId;
        if (!string.IsNullOrWhiteSpace(reference))
        {
            Reference = reference.Trim();
        }
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Cancels the voucher.
    /// </summary>
    public void Cancel(DateTime utcNow)
    {
        if (Status == CompanyVoucherStatus.Paid)
        {
            throw new InvalidOperationException("Cannot cancel a company voucher that has already been Paid.");
        }

        if (Status == CompanyVoucherStatus.Cancelled)
        {
            throw new InvalidOperationException("Company voucher is already Cancelled.");
        }

        Status = CompanyVoucherStatus.Cancelled;
        UpdatedAtUtc = utcNow;
    }
}
