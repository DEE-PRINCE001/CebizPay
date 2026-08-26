using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Erp.Entities;

/// <summary>
/// Domain aggregate root representing an immutable official payment receipt generated upon invoice settlement.
/// </summary>
public sealed class ErpReceipt
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Organization identifier for tenant isolation.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Human-readable receipt number (e.g., REC-2026-0001).</summary>
    public string ReceiptNumber { get; private set; } = string.Empty;

    /// <summary>Linked paid invoice identifier (unique 1-to-1 relationship).</summary>
    public Guid InvoiceId { get; private set; }

    /// <summary>Customer identifier.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Total amount received/receipted.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Payment currency.</summary>
    public Currency Currency { get; private set; } = Currency.NGN;

    /// <summary>Payment settlement date.</summary>
    public DateTime PaymentDate { get; private set; }

    /// <summary>Payment settlement channel (Manual vs. Wallet).</summary>
    public InvoiceSettlementMethod SettlementMethod { get; private set; }

    /// <summary>Transaction reference or payment ledger trace ID.</summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>Optional receipt notes or narrative.</summary>
    public string? Notes { get; private set; }

    /// <summary>Operator user ID who processed the payment.</summary>
    public string CreatedByUserId { get; private set; } = string.Empty;

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    private ErpReceipt() { } // EF Core

    /// <summary>
    /// Initializes a new immutable <see cref="ErpReceipt"/>.
    /// </summary>
    public ErpReceipt(
        Guid organizationId,
        string receiptNumber,
        Guid invoiceId,
        Guid customerId,
        decimal amount,
        DateTime paymentDate,
        InvoiceSettlementMethod settlementMethod,
        string reference,
        string createdByUserId,
        Currency currency = Currency.NGN,
        string? notes = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(receiptNumber))
        {
            throw new ArgumentException("ReceiptNumber is required.", nameof(receiptNumber));
        }

        if (invoiceId == Guid.Empty)
        {
            throw new ArgumentException("InvoiceId cannot be empty.", nameof(invoiceId));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId cannot be empty.", nameof(customerId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Receipt amount must be positive.");
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("Reference is required.", nameof(reference));
        }

        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new ArgumentException("CreatedByUserId cannot be empty.", nameof(createdByUserId));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ReceiptNumber = receiptNumber.Trim().ToUpperInvariant();
        InvoiceId = invoiceId;
        CustomerId = customerId;
        Amount = amount;
        PaymentDate = paymentDate;
        SettlementMethod = settlementMethod;
        Reference = reference.Trim();
        CreatedByUserId = createdByUserId.Trim();
        Currency = currency;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }
}
