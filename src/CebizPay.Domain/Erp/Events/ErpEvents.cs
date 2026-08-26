using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Domain.Erp.Events;

/// <summary>Event raised when an inventory item is created.</summary>
public sealed record InventoryItemCreatedDomainEvent(
    Guid InventoryItemId,
    Guid OrganizationId,
    string Sku,
    string Name,
    string CreatedByUserId,
    DateTime OccurredAtUtc);

/// <summary>Event raised when an inventory item is updated.</summary>
public sealed record InventoryItemUpdatedDomainEvent(
    Guid InventoryItemId,
    Guid OrganizationId,
    string Sku,
    string Name,
    DateTime OccurredAtUtc);

/// <summary>Event raised when an inventory item is deactivated or soft deleted.</summary>
public sealed record InventoryItemDeactivatedDomainEvent(
    Guid InventoryItemId,
    Guid OrganizationId,
    string Sku,
    DateTime OccurredAtUtc);

/// <summary>Event raised when stock is received into inventory.</summary>
public sealed record StockReceivedDomainEvent(
    Guid StockMovementId,
    Guid InventoryItemId,
    Guid OrganizationId,
    decimal Quantity,
    decimal UnitCost,
    string Reference,
    ValuationMethod ValuationMethod,
    int ValuationPolicyVersion,
    DateTime OccurredAtUtc);

/// <summary>Event raised when stock is issued out of inventory.</summary>
public sealed record StockIssuedDomainEvent(
    Guid StockMovementId,
    Guid InventoryItemId,
    Guid OrganizationId,
    decimal Quantity,
    decimal? UnitCost,
    string Reference,
    ValuationMethod ValuationMethod,
    int ValuationPolicyVersion,
    DateTime OccurredAtUtc);

/// <summary>Event raised when stock is adjusted.</summary>
public sealed record StockAdjustedDomainEvent(
    Guid StockMovementId,
    Guid InventoryItemId,
    Guid OrganizationId,
    decimal QuantityDelta,
    string Reference,
    string? Reason,
    DateTime OccurredAtUtc);

/// <summary>Event raised when the organization's active inventory valuation policy changes.</summary>
public sealed record InventoryValuationPolicyChangedDomainEvent(
    Guid PolicyId,
    Guid OrganizationId,
    ValuationMethod NewMethod,
    int NewVersion,
    string ChangedByUserId,
    DateTime OccurredAtUtc);

/// <summary>Event raised when an ERP service is created.</summary>
public sealed record ErpServiceCreatedDomainEvent(
    Guid ServiceId,
    Guid OrganizationId,
    string Code,
    string Name,
    DateTime OccurredAtUtc);

/// <summary>Event raised when an ERP service is updated.</summary>
public sealed record ErpServiceUpdatedDomainEvent(
    Guid ServiceId,
    Guid OrganizationId,
    string Code,
    string Name,
    DateTime OccurredAtUtc);

/// <summary>Event raised when an ERP service is deleted / deactivated.</summary>
public sealed record ErpServiceDeletedDomainEvent(
    Guid ServiceId,
    Guid OrganizationId,
    string Code,
    DateTime OccurredAtUtc);

/// <summary>Event raised when a supplier is created.</summary>
public sealed record SupplierCreatedDomainEvent(
    Guid SupplierId,
    Guid OrganizationId,
    string Reference,
    string Name,
    DateTime OccurredAtUtc);

/// <summary>Event raised when a supplier is updated.</summary>
public sealed record SupplierUpdatedDomainEvent(
    Guid SupplierId,
    Guid OrganizationId,
    string Reference,
    string Name,
    DateTime OccurredAtUtc);

/// <summary>Event raised when a supplier is deleted / deactivated.</summary>
public sealed record SupplierDeletedDomainEvent(
    Guid SupplierId,
    Guid OrganizationId,
    string Reference,
    DateTime OccurredAtUtc);

/// <summary>Event raised when a customer is created.</summary>
public sealed record CustomerCreatedDomainEvent(
    Guid CustomerId,
    Guid OrganizationId,
    string Reference,
    string Name,
    DateTime OccurredAtUtc);

/// <summary>Event raised when a customer is updated.</summary>
public sealed record CustomerUpdatedDomainEvent(
    Guid CustomerId,
    Guid OrganizationId,
    string Reference,
    string Name,
    DateTime OccurredAtUtc);

/// <summary>Event raised when a customer is deleted / deactivated.</summary>
public sealed record CustomerDeletedDomainEvent(
    Guid CustomerId,
    Guid OrganizationId,
    string Reference,
    DateTime OccurredAtUtc);

// Phase 5D Orders, Expenses, Invoices & Receipts Events

/// <summary>Event raised when a purchase order is created.</summary>
public sealed record PurchaseOrderCreatedDomainEvent(
    Guid PurchaseOrderId,
    Guid OrganizationId,
    string OrderNumber,
    Guid SupplierId,
    decimal TotalAmount,
    DateTime OccurredAtUtc);

/// <summary>Event raised when a purchase order is confirmed.</summary>
public sealed record PurchaseOrderConfirmedDomainEvent(
    Guid PurchaseOrderId,
    Guid OrganizationId,
    string OrderNumber,
    DateTime OccurredAtUtc);

/// <summary>Event raised when a purchase order is received into inventory.</summary>
public sealed record PurchaseOrderReceivedDomainEvent(
    Guid PurchaseOrderId,
    Guid OrganizationId,
    string OrderNumber,
    PurchaseOrderStatus Status,
    DateTime OccurredAtUtc);

/// <summary>Event raised when a purchase order is cancelled.</summary>
public sealed record PurchaseOrderCancelledDomainEvent(
    Guid PurchaseOrderId,
    Guid OrganizationId,
    string OrderNumber,
    DateTime OccurredAtUtc);

/// <summary>Event raised when a sales order is created.</summary>
public sealed record SalesOrderCreatedDomainEvent(
    Guid SalesOrderId,
    Guid OrganizationId,
    string OrderNumber,
    Guid CustomerId,
    decimal TotalAmount,
    DateTime OccurredAtUtc);

/// <summary>Event raised when a sales order is confirmed.</summary>
public sealed record SalesOrderConfirmedDomainEvent(
    Guid SalesOrderId,
    Guid OrganizationId,
    string OrderNumber,
    DateTime OccurredAtUtc);

/// <summary>Event raised when a sales order is fulfilled from inventory.</summary>
public sealed record SalesOrderFulfilledDomainEvent(
    Guid SalesOrderId,
    Guid OrganizationId,
    string OrderNumber,
    SalesOrderStatus Status,
    DateTime OccurredAtUtc);

/// <summary>Event raised when a sales order is cancelled.</summary>
public sealed record SalesOrderCancelledDomainEvent(
    Guid SalesOrderId,
    Guid OrganizationId,
    string OrderNumber,
    DateTime OccurredAtUtc);

/// <summary>Event raised when an operating expense is created.</summary>
public sealed record ExpenseCreatedDomainEvent(
    Guid ExpenseId,
    Guid OrganizationId,
    string ExpenseNumber,
    ExpenseCategory Category,
    decimal Amount,
    DateTime OccurredAtUtc);

/// <summary>Event raised when an operating expense is approved.</summary>
public sealed record ExpenseApprovedDomainEvent(
    Guid ExpenseId,
    Guid OrganizationId,
    string ExpenseNumber,
    string ApprovedByUserId,
    DateTime OccurredAtUtc);

/// <summary>Event raised when an operating expense is paid.</summary>
public sealed record ExpensePaidDomainEvent(
    Guid ExpenseId,
    Guid OrganizationId,
    string ExpenseNumber,
    decimal Amount,
    ExpensePaymentMethod PaymentMethod,
    Guid? LedgerTransactionId,
    DateTime OccurredAtUtc);

/// <summary>Event raised when an operating expense is cancelled.</summary>
public sealed record ExpenseCancelledDomainEvent(
    Guid ExpenseId,
    Guid OrganizationId,
    string ExpenseNumber,
    DateTime OccurredAtUtc);

/// <summary>Event raised when an invoice is created.</summary>
public sealed record InvoiceCreatedDomainEvent(
    Guid InvoiceId,
    Guid OrganizationId,
    string InvoiceNumber,
    Guid CustomerId,
    decimal TotalAmount,
    DateTime OccurredAtUtc);

/// <summary>Event raised when an invoice is issued.</summary>
public sealed record InvoiceIssuedDomainEvent(
    Guid InvoiceId,
    Guid OrganizationId,
    string InvoiceNumber,
    DateTime DueDate,
    DateTime OccurredAtUtc);

/// <summary>Event raised when an invoice payment is settled.</summary>
public sealed record InvoicePaidDomainEvent(
    Guid InvoiceId,
    Guid OrganizationId,
    string InvoiceNumber,
    decimal PaidAmount,
    InvoiceSettlementMethod SettlementMethod,
    DateTime OccurredAtUtc);

/// <summary>Event raised when an invoice is cancelled.</summary>
public sealed record InvoiceCancelledDomainEvent(
    Guid InvoiceId,
    Guid OrganizationId,
    string InvoiceNumber,
    DateTime OccurredAtUtc);

/// <summary>Event raised when an immutable payment receipt is generated.</summary>
public sealed record ReceiptGeneratedDomainEvent(
    Guid ReceiptId,
    Guid OrganizationId,
    string ReceiptNumber,
    Guid InvoiceId,
    decimal Amount,
    DateTime OccurredAtUtc);

// ==========================================
// Company Vouchers (Phase 5E)
// ==========================================

/// <summary>Event raised when a company voucher is created.</summary>
public sealed record CompanyVoucherCreatedDomainEvent(
    Guid VoucherId,
    Guid OrganizationId,
    string VoucherNumber,
    decimal Amount,
    Currency Currency,
    string PayeeName,
    DateTime OccurredAtUtc);

/// <summary>Event raised when a company voucher is approved.</summary>
public sealed record CompanyVoucherApprovedDomainEvent(
    Guid VoucherId,
    Guid OrganizationId,
    string VoucherNumber,
    string ApprovedByUserId,
    DateTime OccurredAtUtc);

/// <summary>Event raised when a company voucher is paid.</summary>
public sealed record CompanyVoucherPaidDomainEvent(
    Guid VoucherId,
    Guid OrganizationId,
    string VoucherNumber,
    decimal Amount,
    Currency Currency,
    CompanyVoucherPaymentMethod PaymentMethod,
    Guid? LedgerTransactionId,
    DateTime OccurredAtUtc);

/// <summary>Event raised when a company voucher is cancelled.</summary>
public sealed record CompanyVoucherCancelledDomainEvent(
    Guid VoucherId,
    Guid OrganizationId,
    string VoucherNumber,
    DateTime OccurredAtUtc);

