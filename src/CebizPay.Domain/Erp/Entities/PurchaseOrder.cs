using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Erp.Entities;

/// <summary>
/// Domain aggregate root representing an ERP Purchase Order issued to an external vendor/supplier.
/// </summary>
public sealed class PurchaseOrder
{
    private readonly List<PurchaseOrderItem> _items = new();

    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Organization identifier for tenant isolation.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Unique human-readable order reference (e.g., PO-2026-0001).</summary>
    public string OrderNumber { get; private set; } = string.Empty;

    /// <summary>Vendor/Supplier identifier.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Order creation date.</summary>
    public DateTime OrderDate { get; private set; }

    /// <summary>Expected goods arrival / delivery date.</summary>
    public DateTime? ExpectedDeliveryDate { get; private set; }

    /// <summary>Current lifecycle status.</summary>
    public PurchaseOrderStatus Status { get; private set; } = PurchaseOrderStatus.Draft;

    /// <summary>Total net amount before tax.</summary>
    public decimal Subtotal { get; private set; }

    /// <summary>Total calculated VAT amount (if applicable).</summary>
    public decimal VatAmount { get; private set; }

    /// <summary>Gross total order amount (Subtotal + VatAmount).</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Operational currency.</summary>
    public Currency Currency { get; private set; } = Currency.NGN;

    /// <summary>Additional notes or delivery instructions.</summary>
    public string? Notes { get; private set; }

    /// <summary>Operator user ID who created the purchase order.</summary>
    public string CreatedByUserId { get; private set; } = string.Empty;

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last update timestamp in UTC.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Collection of purchase order line items.</summary>
    public IReadOnlyCollection<PurchaseOrderItem> Items => _items.AsReadOnly();

    private PurchaseOrder() { } // EF Core

    /// <summary>
    /// Initializes a new instance of <see cref="PurchaseOrder"/>.
    /// </summary>
    public PurchaseOrder(
        Guid organizationId,
        string orderNumber,
        Guid supplierId,
        string createdByUserId,
        DateTime orderDate,
        DateTime? expectedDeliveryDate = null,
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

        if (supplierId == Guid.Empty)
        {
            throw new ArgumentException("SupplierId cannot be empty.", nameof(supplierId));
        }

        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new ArgumentException("CreatedByUserId cannot be empty.", nameof(createdByUserId));
        }

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        OrderNumber = orderNumber.Trim().ToUpperInvariant();
        SupplierId = supplierId;
        CreatedByUserId = createdByUserId.Trim();
        OrderDate = orderDate;
        ExpectedDeliveryDate = expectedDeliveryDate;
        Currency = currency;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Status = PurchaseOrderStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a line item to the purchase order.
    /// </summary>
    public PurchaseOrderItem AddItem(
        string description,
        decimal quantity,
        decimal unitPrice,
        Guid? inventoryItemId = null,
        Guid? serviceId = null)
    {
        if (Status != PurchaseOrderStatus.Draft)
        {
            throw new InvalidOperationException("Cannot add items to a non-draft purchase order.");
        }

        var item = new PurchaseOrderItem(
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
    /// Confirms the draft purchase order.
    /// </summary>
    public void Confirm()
    {
        if (Status != PurchaseOrderStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot confirm purchase order in status '{Status}'.");
        }

        if (_items.Count == 0)
        {
            throw new InvalidOperationException("Cannot confirm a purchase order without items.");
        }

        Status = PurchaseOrderStatus.Confirmed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Receives quantities for a purchase order item line and updates order status.
    /// </summary>
    public void ReceiveItemQuantity(Guid itemId, decimal quantityReceived)
    {
        if (Status != PurchaseOrderStatus.Confirmed && Status != PurchaseOrderStatus.PartiallyReceived)
        {
            throw new InvalidOperationException($"Cannot receive items for purchase order in status '{Status}'.");
        }

        var line = _items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new KeyNotFoundException($"Purchase order item '{itemId}' not found.");

        line.ReceiveQuantity(quantityReceived);

        // Update aggregate status
        var allFullyReceived = _items.All(i => i.ReceivedQuantity >= i.Quantity);
        Status = allFullyReceived ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Cancels the purchase order.
    /// </summary>
    public void Cancel()
    {
        if (Status == PurchaseOrderStatus.Received)
        {
            throw new InvalidOperationException("Cannot cancel a fully received purchase order.");
        }

        Status = PurchaseOrderStatus.Cancelled;
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
/// Domain entity representing an individual item line on a purchase order.
/// </summary>
public sealed class PurchaseOrderItem
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Parent purchase order identifier.</summary>
    public Guid PurchaseOrderId { get; private set; }

    /// <summary>Optional linked inventory item identifier.</summary>
    public Guid? InventoryItemId { get; private set; }

    /// <summary>Optional linked ERP service identifier.</summary>
    public Guid? ServiceId { get; private set; }

    /// <summary>Item description or line narrative.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Ordered quantity.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>Quantity received to date.</summary>
    public decimal ReceivedQuantity { get; private set; }

    /// <summary>Unit price agreed with supplier.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Total line amount (Quantity * UnitPrice).</summary>
    public decimal TotalAmount { get; private set; }

    private PurchaseOrderItem() { } // EF Core

    /// <summary>
    /// Initializes a new instance of <see cref="PurchaseOrderItem"/>.
    /// </summary>
    public PurchaseOrderItem(
        Guid purchaseOrderId,
        string description,
        decimal quantity,
        decimal unitPrice,
        Guid? inventoryItemId = null,
        Guid? serviceId = null)
    {
        if (purchaseOrderId == Guid.Empty)
        {
            throw new ArgumentException("PurchaseOrderId cannot be empty.", nameof(purchaseOrderId));
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
        PurchaseOrderId = purchaseOrderId;
        Description = description.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        TotalAmount = Math.Round(quantity * unitPrice, 2);
        InventoryItemId = inventoryItemId;
        ServiceId = serviceId;
        ReceivedQuantity = 0;
    }

    /// <summary>
    /// Records received quantity on this item line.
    /// </summary>
    public void ReceiveQuantity(decimal quantityReceived)
    {
        if (quantityReceived <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantityReceived), "Received quantity must be greater than zero.");
        }

        if (ReceivedQuantity + quantityReceived > Quantity)
        {
            throw new InvalidOperationException($"Cannot receive {quantityReceived}. Ordered: {Quantity}, already received: {ReceivedQuantity}.");
        }

        ReceivedQuantity += quantityReceived;
    }
}
