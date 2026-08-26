using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Erp.Entities;

/// <summary>
/// Domain aggregate root representing an ERP Sales Order from a customer.
/// </summary>
public sealed class SalesOrder
{
    private readonly List<SalesOrderItem> _items = new();

    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Organization identifier for tenant isolation.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Unique human-readable sales order number (e.g., SO-2026-0001).</summary>
    public string OrderNumber { get; private set; } = string.Empty;

    /// <summary>Customer identifier.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Order placement date.</summary>
    public DateTime OrderDate { get; private set; }

    /// <summary>Expected order fulfillment / dispatch date.</summary>
    public DateTime? ExpectedFulfillmentDate { get; private set; }

    /// <summary>Current lifecycle status.</summary>
    public SalesOrderStatus Status { get; private set; } = SalesOrderStatus.Draft;

    /// <summary>Total net amount before VAT.</summary>
    public decimal Subtotal { get; private set; }

    /// <summary>Total VAT amount.</summary>
    public decimal VatAmount { get; private set; }

    /// <summary>Gross total sales order amount (Subtotal + VatAmount).</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Operational currency.</summary>
    public Currency Currency { get; private set; } = Currency.NGN;

    /// <summary>Additional customer notes or dispatch instructions.</summary>
    public string? Notes { get; private set; }

    /// <summary>Operator user ID who created the sales order.</summary>
    public string CreatedByUserId { get; private set; } = string.Empty;

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last update timestamp in UTC.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Collection of sales order line items.</summary>
    public IReadOnlyCollection<SalesOrderItem> Items => _items.AsReadOnly();

    private SalesOrder() { } // EF Core

    /// <summary>
    /// Initializes a new instance of <see cref="SalesOrder"/>.
    /// </summary>
    public SalesOrder(
        Guid organizationId,
        string orderNumber,
        Guid customerId,
        string createdByUserId,
        DateTime orderDate,
        DateTime? expectedFulfillmentDate = null,
        Currency currency = Currency.NGN,
        string? notes = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("OrganizationId cannot be empty.", nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            throw new ArgumentException("OrderNumber is required.", nameof(orderNumber));
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
        OrderNumber = orderNumber.Trim().ToUpperInvariant();
        CustomerId = customerId;
        CreatedByUserId = createdByUserId.Trim();
        OrderDate = orderDate;
        ExpectedFulfillmentDate = expectedFulfillmentDate;
        Currency = currency;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Status = SalesOrderStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a line item to the sales order.
    /// </summary>
    public SalesOrderItem AddItem(
        string description,
        decimal quantity,
        decimal unitPrice,
        Guid? inventoryItemId = null,
        Guid? serviceId = null)
    {
        if (Status != SalesOrderStatus.Draft)
        {
            throw new InvalidOperationException("Cannot add items to a non-draft sales order.");
        }

        var item = new SalesOrderItem(
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
    /// Confirms the draft sales order.
    /// </summary>
    public void Confirm()
    {
        if (Status != SalesOrderStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot confirm sales order in status '{Status}'.");
        }

        if (_items.Count == 0)
        {
            throw new InvalidOperationException("Cannot confirm a sales order without items.");
        }

        Status = SalesOrderStatus.Confirmed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Fulfills quantities for a sales order item line and updates aggregate status.
    /// </summary>
    public void FulfillItemQuantity(Guid itemId, decimal quantityFulfilled)
    {
        if (Status != SalesOrderStatus.Confirmed && Status != SalesOrderStatus.PartiallyFulfilled)
        {
            throw new InvalidOperationException($"Cannot fulfill items for sales order in status '{Status}'.");
        }

        var line = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new KeyNotFoundException($"Sales order item '{itemId}' not found.");

        line.FulfillQuantity(quantityFulfilled);

        var allFullyFulfilled = _items.All(i => i.FulfilledQuantity >= i.Quantity);
        Status = allFullyFulfilled ? SalesOrderStatus.Fulfilled : SalesOrderStatus.PartiallyFulfilled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Cancels the sales order.
    /// </summary>
    public void Cancel()
    {
        if (Status == SalesOrderStatus.Fulfilled)
        {
            throw new InvalidOperationException("Cannot cancel a fully fulfilled sales order.");
        }

        Status = SalesOrderStatus.Cancelled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Recalculates subtotal and total amount.
    /// </summary>
    public void RecalculateTotals()
    {
        Subtotal = Math.Round(_items.Sum(i => i.TotalAmount), 2);
        TotalAmount = Subtotal + VatAmount;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

/// <summary>
/// Domain entity representing an individual item line on a sales order.
/// </summary>
public sealed class SalesOrderItem
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Parent sales order identifier.</summary>
    public Guid SalesOrderId { get; private set; }

    /// <summary>Optional linked inventory item identifier.</summary>
    public Guid? InventoryItemId { get; private set; }

    /// <summary>Optional linked ERP service identifier.</summary>
    public Guid? ServiceId { get; private set; }

    /// <summary>Item description or line narrative.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Ordered quantity.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>Quantity fulfilled/dispatched to date.</summary>
    public decimal FulfilledQuantity { get; private set; }

    /// <summary>Selling unit price.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Total line amount (Quantity * UnitPrice).</summary>
    public decimal TotalAmount { get; private set; }

    private SalesOrderItem() { } // EF Core

    /// <summary>
    /// Initializes a new instance of <see cref="SalesOrderItem"/>.
    /// </summary>
    public SalesOrderItem(
        Guid salesOrderId,
        string description,
        decimal quantity,
        decimal unitPrice,
        Guid? inventoryItemId = null,
        Guid? serviceId = null)
    {
        if (salesOrderId == Guid.Empty)
        {
            throw new ArgumentException("SalesOrderId cannot be empty.", nameof(salesOrderId));
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
        SalesOrderId = salesOrderId;
        Description = description.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        TotalAmount = Math.Round(quantity * unitPrice, 2);
        InventoryItemId = inventoryItemId;
        ServiceId = serviceId;
        FulfilledQuantity = 0;
    }

    /// <summary>
    /// Records fulfilled quantity on this line.
    /// </summary>
    public void FulfillQuantity(decimal quantityFulfilled)
    {
        if (quantityFulfilled <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantityFulfilled), "Fulfilled quantity must be greater than zero.");
        }

        if (FulfilledQuantity + quantityFulfilled > Quantity)
        {
            throw new InvalidOperationException($"Cannot fulfill {quantityFulfilled}. Ordered: {Quantity}, already fulfilled: {FulfilledQuantity}.");
        }

        FulfilledQuantity += quantityFulfilled;
    }
}
