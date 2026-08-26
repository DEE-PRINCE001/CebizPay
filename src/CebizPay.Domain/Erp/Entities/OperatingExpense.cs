using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Erp.Entities;

/// <summary>
/// Domain aggregate root representing an operating expenditure of an organization.
/// </summary>
public sealed class OperatingExpense
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Organization identifier for tenant isolation.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Human-readable expense voucher/reference number (e.g., EXP-2026-0001).</summary>
    public string ExpenseNumber { get; private set; } = string.Empty;

    /// <summary>Classification category (Rent, Utilities, Marketing, etc.).</summary>
    public ExpenseCategory Category { get; private set; }

    /// <summary>Expense description / narrative.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Amount of expense.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Currency code.</summary>
    public Currency Currency { get; private set; } = Currency.NGN;

    /// <summary>Date expense was incurred.</summary>
    public DateTime ExpenseDate { get; private set; }

    /// <summary>Optional linked vendor/supplier.</summary>
    public Guid? SupplierId { get; private set; }

    /// <summary>Payment settlement method (Manual vs. Wallet).</summary>
    public ExpensePaymentMethod PaymentMethod { get; private set; }

    /// <summary>Current lifecycle status.</summary>
    public ExpenseStatus Status { get; private set; } = ExpenseStatus.Draft;

    /// <summary>Source wallet identifier (if paid from organization wallet).</summary>
    public Guid? WalletId { get; private set; }

    /// <summary>Central ledger transaction identifier (if settled via wallet double-entry).</summary>
    public Guid? LedgerTransactionId { get; private set; }

    /// <summary>Payment external reference or bank transaction narrative.</summary>
    public string? Reference { get; private set; }

    /// <summary>User ID who created the expense.</summary>
    public string CreatedByUserId { get; private set; } = string.Empty;

    /// <summary>User ID who approved the expense.</summary>
    public string? ApprovedByUserId { get; private set; }

    /// <summary>Timestamp when expense was approved.</summary>
    public DateTime? ApprovedAtUtc { get; private set; }

    /// <summary>Timestamp when expense was paid/settled.</summary>
    public DateTime? PaidAtUtc { get; private set; }

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last update timestamp in UTC.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    private OperatingExpense() { } // EF Core

    /// <summary>
    /// Initializes a new instance of <see cref="OperatingExpense"/>.
    /// </summary>
    public OperatingExpense(
        Guid organizationId,
        string expenseNumber,
        ExpenseCategory category,
        string description,
        decimal amount,
        DateTime expenseDate,
        string createdByUserId,
        ExpensePaymentMethod paymentMethod = ExpensePaymentMethod.Manual,
        Guid? supplierId = null,
        Currency currency = Currency.NGN,
        string? reference = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(expenseNumber))
        {
            throw new ArgumentException("ExpenseNumber is required.", nameof(expenseNumber));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required.", nameof(description));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Expense amount must be positive.");
        }

        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new ArgumentException("CreatedByUserId cannot be empty.", nameof(createdByUserId));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        ExpenseNumber = expenseNumber.Trim().ToUpperInvariant();
        Category = category;
        Description = description.Trim();
        Amount = amount;
        ExpenseDate = expenseDate;
        CreatedByUserId = createdByUserId.Trim();
        PaymentMethod = paymentMethod;
        SupplierId = supplierId;
        Currency = currency;
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        Status = ExpenseStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Approves the operating expense for disbursement / payment.
    /// </summary>
    public void Approve(string approvedByUserId, DateTime utcNow)
    {
        if (Status != ExpenseStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot approve expense in status '{Status}'.");
        }

        if (string.IsNullOrWhiteSpace(approvedByUserId))
        {
            throw new ArgumentException("ApprovedByUserId cannot be empty.", nameof(approvedByUserId));
        }

        Status = ExpenseStatus.Approved;
        ApprovedByUserId = approvedByUserId.Trim();
        ApprovedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Marks the approved expense as Paid.
    /// </summary>
    public void MarkPaid(DateTime utcNow, Guid? walletId = null, Guid? ledgerTransactionId = null, string? reference = null)
    {
        if (Status != ExpenseStatus.Approved)
        {
            throw new InvalidOperationException($"Cannot pay expense in status '{Status}'. It must be Approved first.");
        }

        Status = ExpenseStatus.Paid;
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
    /// Cancels the expense.
    /// </summary>
    public void Cancel(DateTime utcNow)
    {
        if (Status == ExpenseStatus.Paid)
        {
            throw new InvalidOperationException("Cannot cancel an already paid expense.");
        }

        Status = ExpenseStatus.Cancelled;
        UpdatedAtUtc = utcNow;
    }
}
