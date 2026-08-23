using CebizPay.Domain.Erp.Enums;

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
