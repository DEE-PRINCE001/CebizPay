using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.UseCases.Organizations.Erp;

/// <summary>
/// DTO representing an inventory item.
/// </summary>
public sealed record InventoryItemDto(
    Guid Id,
    Guid OrganizationId,
    string Sku,
    string Name,
    string? Description,
    string? Category,
    string UnitOfMeasure,
    Currency Currency,
    decimal CurrentQuantity,
    decimal ReorderLevel,
    decimal CurrentAverageCost,
    decimal SellingPrice,
    decimal TotalValuation,
    StockStatus StockStatus,
    InventoryItemStatus Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// DTO representing a stock movement transaction.
/// </summary>
public sealed record StockMovementDto(
    Guid Id,
    Guid OrganizationId,
    Guid InventoryItemId,
    string ItemName,
    string ItemSku,
    StockMovementType MovementType,
    decimal Quantity,
    decimal? UnitCost,
    decimal? TotalCost,
    string Reference,
    string? Reason,
    ValuationMethod ValuationMethod,
    int ValuationPolicyVersion,
    string CreatedByUserId,
    DateTime CreatedAtUtc);

/// <summary>
/// DTO representing an inventory valuation policy version.
/// </summary>
public sealed record InventoryValuationPolicyDto(
    Guid Id,
    Guid OrganizationId,
    ValuationMethod Method,
    int Version,
    DateTime EffectiveFromUtc,
    DateTime? DeactivatedAtUtc,
    bool IsActive,
    string CreatedByUserId,
    DateTime CreatedAtUtc);

/// <summary>
/// DTO representing an ERP billable service.
/// </summary>
public sealed record ErpServiceDto(
    Guid Id,
    Guid OrganizationId,
    string Code,
    string Name,
    string? Description,
    decimal UnitPrice,
    Currency Currency,
    ErpServiceStatus Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// DTO representing an ERP supplier.
/// </summary>
public sealed record SupplierDto(
    Guid Id,
    Guid OrganizationId,
    string Reference,
    string Name,
    string? Email,
    string? Phone,
    string? Address,
    string? TaxIdentifier,
    SupplierStatus Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// DTO representing an ERP customer.
/// </summary>
public sealed record CustomerDto(
    Guid Id,
    Guid OrganizationId,
    string Reference,
    string Name,
    string? Email,
    string? Phone,
    string? Address,
    CustomerStatus Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// API request payload to create an inventory item.
/// </summary>
public sealed record CreateInventoryItemApiRequest(
    string Sku,
    string Name,
    string UnitOfMeasure,
    decimal SellingPrice,
    string? Description = null,
    string? Category = null,
    decimal ReorderLevel = 0,
    Currency Currency = Currency.NGN,
    decimal InitialQuantity = 0,
    decimal InitialUnitCost = 0);

/// <summary>
/// API request payload to update an inventory item.
/// </summary>
public sealed record UpdateInventoryItemApiRequest(
    string Name,
    string UnitOfMeasure,
    decimal SellingPrice,
    string? Description = null,
    string? Category = null,
    decimal ReorderLevel = 0);

/// <summary>
/// API request payload to receive incoming stock.
/// </summary>
public sealed record StockInApiRequest(
    decimal Quantity,
    decimal UnitCost,
    string Reference,
    string? Reason = null);

/// <summary>
/// API request payload to issue outgoing stock.
/// </summary>
public sealed record StockOutApiRequest(
    decimal Quantity,
    string Reference,
    string? Reason = null);

/// <summary>
/// API request payload to manually adjust stock quantity.
/// </summary>
public sealed record StockAdjustmentApiRequest(
    decimal QuantityDelta,
    string Reference,
    string Reason,
    decimal? NewAverageCost = null);

/// <summary>
/// API request payload to activate a new inventory valuation policy method.
/// </summary>
public sealed record SetValuationPolicyApiRequest(
    ValuationMethod Method);

/// <summary>
/// API request payload to create an ERP service.
/// </summary>
public sealed record CreateErpServiceApiRequest(
    string Code,
    string Name,
    decimal UnitPrice,
    string? Description = null,
    Currency Currency = Currency.NGN);

/// <summary>
/// API request payload to update an ERP service.
/// </summary>
public sealed record UpdateErpServiceApiRequest(
    string Name,
    decimal UnitPrice,
    string? Description = null);

/// <summary>
/// API request payload to create an ERP supplier.
/// </summary>
public sealed record CreateSupplierApiRequest(
    string Reference,
    string Name,
    string? Email = null,
    string? Phone = null,
    string? Address = null,
    string? TaxIdentifier = null);

/// <summary>
/// API request payload to update an ERP supplier.
/// </summary>
public sealed record UpdateSupplierApiRequest(
    string Name,
    string? Email = null,
    string? Phone = null,
    string? Address = null,
    string? TaxIdentifier = null);

/// <summary>
/// API request payload to create an ERP customer.
/// </summary>
public sealed record CreateCustomerApiRequest(
    string Reference,
    string Name,
    string? Email = null,
    string? Phone = null,
    string? Address = null);

/// <summary>
/// API request payload to update an ERP customer.
/// </summary>
public sealed record UpdateCustomerApiRequest(
    string Name,
    string? Email = null,
    string? Phone = null,
    string? Address = null);
