#pragma warning disable CS1591
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.UseCases.Organizations.Erp;

// ==========================================
// DTOs
// ==========================================

/// <summary>
/// DTO representing an ERP Purchase Order.
/// </summary>
public sealed record PurchaseOrderDto(
    Guid Id,
    Guid OrganizationId,
    string OrderNumber,
    Guid SupplierId,
    DateTime OrderDate,
    DateTime? ExpectedDeliveryDate,
    PurchaseOrderStatus Status,
    decimal Subtotal,
    decimal VatAmount,
    decimal TotalAmount,
    Currency Currency,
    string? Notes,
    string CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyCollection<PurchaseOrderItemDto> Items);

/// <summary>
/// DTO representing a line item on an ERP Purchase Order.
/// </summary>
public sealed record PurchaseOrderItemDto(
    Guid Id,
    Guid PurchaseOrderId,
    Guid? InventoryItemId,
    Guid? ServiceId,
    string Description,
    decimal Quantity,
    decimal ReceivedQuantity,
    decimal UnitPrice,
    decimal TotalAmount);

/// <summary>
/// DTO representing an ERP Sales Order.
/// </summary>
public sealed record SalesOrderDto(
    Guid Id,
    Guid OrganizationId,
    string OrderNumber,
    Guid CustomerId,
    DateTime OrderDate,
    DateTime? ExpectedFulfillmentDate,
    SalesOrderStatus Status,
    decimal Subtotal,
    decimal VatAmount,
    decimal TotalAmount,
    Currency Currency,
    string? Notes,
    string CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyCollection<SalesOrderItemDto> Items);

/// <summary>
/// DTO representing a line item on an ERP Sales Order.
/// </summary>
public sealed record SalesOrderItemDto(
    Guid Id,
    Guid SalesOrderId,
    Guid? InventoryItemId,
    Guid? ServiceId,
    string Description,
    decimal Quantity,
    decimal FulfilledQuantity,
    decimal UnitPrice,
    decimal TotalAmount);

/// <summary>
/// DTO representing an operating expense.
/// </summary>
public sealed record OperatingExpenseDto(
    Guid Id,
    Guid OrganizationId,
    string ExpenseNumber,
    ExpenseCategory Category,
    string Description,
    decimal Amount,
    Currency Currency,
    DateTime ExpenseDate,
    Guid? SupplierId,
    ExpensePaymentMethod PaymentMethod,
    ExpenseStatus Status,
    Guid? WalletId,
    Guid? LedgerTransactionId,
    string? Reference,
    string CreatedByUserId,
    string? ApprovedByUserId,
    DateTime? ApprovedAtUtc,
    DateTime? PaidAtUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// DTO representing an ERP Invoice.
/// </summary>
public sealed record ErpInvoiceDto(
    Guid Id,
    Guid OrganizationId,
    string InvoiceNumber,
    Guid CustomerId,
    Guid? SalesOrderId,
    DateTime IssueDate,
    DateTime DueDate,
    bool ApplyVat,
    decimal VatRate,
    decimal Subtotal,
    decimal VatAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    Currency Currency,
    InvoiceStatus Status,
    InvoiceSettlementMethod SettlementMethod,
    string? Notes,
    string? BillingContact,
    string CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyCollection<ErpInvoiceItemDto> Items);

/// <summary>
/// DTO representing a line item on an ERP Invoice.
/// </summary>
public sealed record ErpInvoiceItemDto(
    Guid Id,
    Guid ErpInvoiceId,
    Guid? InventoryItemId,
    Guid? ServiceId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalAmount);

/// <summary>
/// DTO representing an immutable payment receipt.
/// </summary>
public sealed record ErpReceiptDto(
    Guid Id,
    Guid OrganizationId,
    string ReceiptNumber,
    Guid InvoiceId,
    Guid CustomerId,
    decimal Amount,
    Currency Currency,
    DateTime PaymentDate,
    InvoiceSettlementMethod SettlementMethod,
    string Reference,
    string? Notes,
    string CreatedByUserId,
    DateTime CreatedAtUtc);

// ==========================================
// API Requests
// ==========================================

/// <summary>Request payload to create a purchase order.</summary>
public sealed record CreatePurchaseOrderApiRequest(
    Guid SupplierId,
    DateTime OrderDate,
    DateTime? ExpectedDeliveryDate,
    Currency Currency,
    string? Notes,
    List<PurchaseOrderItemRequest> Items);

/// <summary>Request item for purchase order creation.</summary>
public sealed record PurchaseOrderItemRequest(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    Guid? InventoryItemId = null,
    Guid? ServiceId = null);

/// <summary>Request payload to receive goods on a purchase order line.</summary>
public sealed record ReceivePurchaseOrderItemApiRequest(
    decimal QuantityReceived);

/// <summary>Request payload to create a sales order.</summary>
public sealed record CreateSalesOrderApiRequest(
    Guid CustomerId,
    DateTime OrderDate,
    DateTime? ExpectedFulfillmentDate,
    Currency Currency,
    string? Notes,
    List<SalesOrderItemRequest> Items);

/// <summary>Request item for sales order creation.</summary>
public sealed record SalesOrderItemRequest(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    Guid? InventoryItemId = null,
    Guid? ServiceId = null);

/// <summary>Request payload to fulfill goods on a sales order line.</summary>
public sealed record FulfillSalesOrderItemApiRequest(
    decimal QuantityFulfilled);

/// <summary>Request payload to create an operating expense.</summary>
public sealed record CreateOperatingExpenseApiRequest(
    ExpenseCategory Category,
    string Description,
    decimal Amount,
    DateTime ExpenseDate,
    ExpensePaymentMethod PaymentMethod = ExpensePaymentMethod.Manual,
    Guid? SupplierId = null,
    Currency Currency = Currency.NGN,
    string? Reference = null);

/// <summary>Request payload to pay an operating expense.</summary>
public sealed record PayOperatingExpenseApiRequest(
    ExpensePaymentMethod PaymentMethod,
    string? Pin = null,
    string? IdempotencyKey = null,
    string? Reference = null);

/// <summary>Request payload to create an invoice.</summary>
public sealed record CreateInvoiceApiRequest(
    Guid CustomerId,
    DateTime IssueDate,
    DateTime DueDate,
    bool ApplyVat,
    Guid? SalesOrderId,
    Currency Currency,
    string? Notes,
    string? BillingContact,
    List<InvoiceItemRequest> Items);

/// <summary>Request item for invoice creation.</summary>
public sealed record InvoiceItemRequest(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    Guid? InventoryItemId = null,
    Guid? ServiceId = null);

/// <summary>Request payload to record invoice payment / settlement.</summary>
public sealed record RecordInvoicePaymentApiRequest(
    decimal Amount,
    InvoiceSettlementMethod SettlementMethod,
    string Reference,
    string? Pin = null,
    string? IdempotencyKey = null);
