#pragma warning disable CS1591
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Finance.Enums;

namespace CebizPay.Application.UseCases.Organizations.Erp;

// ==========================================
// Sales Report DTOs
// ==========================================

public sealed record CurrencySalesSummaryDto(
    Currency Currency,
    decimal TotalGrossSales,
    decimal TotalVatAmount,
    decimal TotalNetSales,
    int OrderCount);

public sealed record CustomerSalesSummaryDto(
    Guid CustomerId,
    string CustomerName,
    int OrderCount,
    decimal TotalAmount,
    Currency Currency);

public sealed record ItemSalesSummaryDto(
    string ItemName,
    decimal QuantitySold,
    decimal TotalRevenue,
    Currency Currency);

public sealed record SalesReportDto(
    Guid OrganizationId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int TotalOrdersCount,
    int DraftOrdersCount,
    int ConfirmedOrdersCount,
    int PartiallyFulfilledOrdersCount,
    int FulfilledOrdersCount,
    int CancelledOrdersCount,
    IReadOnlyList<CurrencySalesSummaryDto> CurrencySummaries,
    IReadOnlyList<CustomerSalesSummaryDto> TopCustomers,
    IReadOnlyList<ItemSalesSummaryDto> ItemSales);

// ==========================================
// Purchase Report DTOs
// ==========================================

public sealed record CurrencyPurchaseSummaryDto(
    Currency Currency,
    decimal TotalPurchasesAmount,
    int OrderCount);

public sealed record SupplierPurchaseSummaryDto(
    Guid SupplierId,
    string SupplierName,
    int OrderCount,
    decimal TotalAmount,
    Currency Currency);

public sealed record ItemPurchaseSummaryDto(
    string ItemName,
    decimal QuantityOrdered,
    decimal QuantityReceived,
    decimal TotalCost,
    Currency Currency);

public sealed record PurchaseReportDto(
    Guid OrganizationId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int TotalOrdersCount,
    int DraftOrdersCount,
    int ConfirmedOrdersCount,
    int PartiallyReceivedOrdersCount,
    int ReceivedOrdersCount,
    int CancelledOrdersCount,
    IReadOnlyList<CurrencyPurchaseSummaryDto> CurrencySummaries,
    IReadOnlyList<SupplierPurchaseSummaryDto> TopSuppliers,
    IReadOnlyList<ItemPurchaseSummaryDto> ItemPurchases);

// ==========================================
// Settlement Report DTOs
// ==========================================

public sealed record CurrencySettlementSummaryDto(
    Currency Currency,
    decimal TotalWalletSettlements,
    decimal TotalManualSettlements,
    decimal GrandTotal,
    int SettlementCount);

public sealed record SettlementItemDto(
    string SettlementType,
    Guid DocumentId,
    string DocumentNumber,
    string SettlementMethod,
    decimal Amount,
    Currency Currency,
    DateTime SettlementDateUtc,
    Guid? LedgerTransactionId,
    string? Reference,
    string? PartyName);

public sealed record SettlementReportDto(
    Guid OrganizationId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    IReadOnlyList<CurrencySettlementSummaryDto> CurrencySummaries,
    PagedResult<SettlementItemDto> Settlements);

// ==========================================
// Profit & Loss Report DTOs
// ==========================================

public sealed record ExpenseCategoryBreakdownDto(
    string Category,
    decimal Amount);

public sealed record CurrencyProfitLossSummaryDto(
    Currency Currency,
    decimal TotalRevenue,
    decimal TotalCogs,
    decimal GrossProfit,
    decimal OperatingExpenses,
    decimal CompanyVoucherDisbursements,
    decimal TotalExpenses,
    decimal NetProfitLoss,
    IReadOnlyList<ExpenseCategoryBreakdownDto> ExpenseBreakdown);

public sealed record ProfitLossReportDto(
    Guid OrganizationId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    IReadOnlyList<CurrencyProfitLossSummaryDto> CurrencySummaries);
