using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Erp.Entities;

/// <summary>
/// Domain aggregate root representing an ERP Invoice issued to a customer.
/// </summary>
public sealed class ErpInvoice
{
    private readonly List<ErpInvoiceItem> _items = new();

    /// <summary>Locked Nigerian statutory VAT rate (7.5%).</summary>
    public const decimal StatutoryVatRate = 0.075m;

    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Organization identifier for tenant isolation.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Human-readable invoice number (e.g., INV-2026-0001).</summary>
    public string InvoiceNumber { get; private set; } = string.Empty;

    /// <summary>Customer identifier.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Optional linked sales order identifier.</summary>
    public Guid? SalesOrderId { get; private set; }

    /// <summary>Invoice issue date.</summary>
    public DateTime IssueDate { get; private set; }

    /// <summary>Invoice payment due date.</summary>
    public DateTime DueDate { get; private set; }

    /// <summary>Whether 7.5% VAT calculation is applied.</summary>
    public bool ApplyVat { get; private set; }

    /// <summary>Applied VAT rate (0.075 when ApplyVat = true, else 0).</summary>
    public decimal VatRate { get; private set; }

    /// <summary>Net subtotal of line items.</summary>
    public decimal Subtotal { get; private set; }

    /// <summary>Calculated VAT amount (Math.Round(Subtotal * 0.075, 2) when ApplyVat = true).</summary>
    public decimal VatAmount { get; private set; }

    /// <summary>Gross total invoice amount (Subtotal + VatAmount).</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Cumulative amount paid to date.</summary>
    public decimal PaidAmount { get; private set; }

    /// <summary>Operational billing currency.</summary>
    public Currency Currency { get; private set; } = Currency.NGN;

    /// <summary>Current invoice status.</summary>
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;

    /// <summary>Settlement method used (Manual vs. Wallet).</summary>
    public InvoiceSettlementMethod SettlementMethod { get; private set; } = InvoiceSettlementMethod.Manual;

    /// <summary>Additional invoice notes / payment terms.</summary>
    public string? Notes { get; private set; }

    /// <summary>Customer contact / billing department.</summary>
    public string? BillingContact { get; private set; }

    /// <summary>User ID who created the invoice.</summary>
    public string CreatedByUserId { get; private set; } = string.Empty;

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last update timestamp in UTC.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Collection of invoice line items.</summary>
    public IReadOnlyCollection<ErpInvoiceItem> Items => _items.AsReadOnly();

    private ErpInvoice() { } // EF Core

    /// <summary>
    /// Initializes a new instance of <see cref="ErpInvoice"/>.
    /// </summary>
    public ErpInvoice(
        Guid organizationId,
        string invoiceNumber,
        Guid customerId,
        string createdByUserId,
        DateTime issueDate,
        DateTime dueDate,
        bool applyVat = true,
        Guid? salesOrderId = null,
        Currency currency = Currency.NGN,
        string? notes = null,
        string? billingContact = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            throw new ArgumentException("InvoiceNumber is required.", nameof(invoiceNumber));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId cannot be empty.", nameof(customerId));
        }

        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new ArgumentException("CreatedByUserId cannot be empty.", nameof(createdByUserId));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        InvoiceNumber = invoiceNumber.Trim().ToUpperInvariant();
        CustomerId = customerId;
        CreatedByUserId = createdByUserId.Trim();
        IssueDate = issueDate;
        DueDate = dueDate;
        ApplyVat = applyVat;
        VatRate = applyVat ? StatutoryVatRate : 0m;
        SalesOrderId = salesOrderId;
        Currency = currency;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        BillingContact = string.IsNullOrWhiteSpace(billingContact) ? null : billingContact.Trim();
        Status = InvoiceStatus.Draft;
        PaidAmount = 0m;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a line item to the invoice.
    /// </summary>
    public ErpInvoiceItem AddItem(
        string description,
        decimal quantity,
        decimal unitPrice,
        Guid? inventoryItemId = null,
        Guid? serviceId = null)
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException("Cannot add items to a non-draft invoice.");
        }

        var item = new ErpInvoiceItem(
            Id,
            description,
            quantity,
            unitPrice,
            inventoryItemId,
            serviceId);

        _items.Add(item);
        RecalculateTotals();
        return item;
    }

    /// <summary>
    /// Issues the invoice to the customer.
    /// </summary>
    public void Issue(DateTime utcNow)
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot issue invoice in status '{Status}'.");
        }

        if (_items.Count == 0)
        {
            throw new InvalidOperationException("Cannot issue an invoice without line items.");
        }

        Status = InvoiceStatus.Issued;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Records a payment towards this invoice and updates its status.
    /// </summary>
    public void RecordPayment(decimal paymentAmount, InvoiceSettlementMethod settlementMethod, DateTime utcNow)
    {
        if (Status != InvoiceStatus.Issued && Status != InvoiceStatus.PartiallyPaid && Status != InvoiceStatus.Overdue)
        {
            throw new InvalidOperationException($"Cannot record payment for invoice in status '{Status}'.");
        }

        if (paymentAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentAmount), "Payment amount must be positive.");
        }

        if (PaidAmount + paymentAmount > TotalAmount)
        {
            throw new InvalidOperationException($"Payment amount {paymentAmount} exceeds outstanding balance of {TotalAmount - PaidAmount}.");
        }

        PaidAmount += paymentAmount;
        SettlementMethod = settlementMethod;

        if (PaidAmount >= TotalAmount)
        {
            Status = InvoiceStatus.Paid;
        }
        else
        {
            Status = InvoiceStatus.PartiallyPaid;
        }

        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Cancels the invoice.
    /// </summary>
    public void Cancel(DateTime utcNow)
    {
        if (Status == InvoiceStatus.Paid)
        {
            throw new InvalidOperationException("Cannot cancel an already paid invoice.");
        }

        Status = InvoiceStatus.Cancelled;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Recalculates subtotal, VAT, and gross total.
    /// </summary>
    public void RecalculateTotals()
    {
        Subtotal = Math.Round(_items.Sum(i => i.TotalAmount), 2);
        VatAmount = ApplyVat ? Math.Round(Subtotal * StatutoryVatRate, 2) : 0m;
        TotalAmount = Subtotal + VatAmount;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// Domain entity representing a line item on an ERP Invoice.
/// </summary>
public sealed class ErpInvoiceItem
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Parent invoice identifier.</summary>
    public Guid ErpInvoiceId { get; private set; }

    /// <summary>Optional linked inventory item identifier.</summary>
    public Guid? InventoryItemId { get; private set; }

    /// <summary>Optional linked ERP service identifier.</summary>
    public Guid? ServiceId { get; private set; }

    /// <summary>Line item narrative / description.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Billed quantity.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>Unit price.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Total line amount (Quantity * UnitPrice).</summary>
    public decimal TotalAmount { get; private set; }

    private ErpInvoiceItem() { } // EF Core

    /// <summary>
    /// Initializes a new instance of <see cref="ErpInvoiceItem"/>.
    /// </summary>
    public ErpInvoiceItem(
        Guid erpInvoiceId,
        string description,
        decimal quantity,
        decimal unitPrice,
        Guid? inventoryItemId = null,
        Guid? serviceId = null)
    {
        if (erpInvoiceId == Guid.Empty)
        {
            throw new ArgumentException("ErpInvoiceId cannot be empty.", nameof(erpInvoiceId));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required.", nameof(description));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "UnitPrice cannot be negative.");
        }

        Id = Guid.NewGuid();
        ErpInvoiceId = erpInvoiceId;
        Description = description.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        TotalAmount = Math.Round(quantity * unitPrice, 2);
        InventoryItemId = inventoryItemId;
        ServiceId = serviceId;
    }
}
