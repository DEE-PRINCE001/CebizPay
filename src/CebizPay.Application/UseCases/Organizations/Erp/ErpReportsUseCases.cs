#pragma warning disable CS1591, CA1862, CA1304, CA1311
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Finance.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Erp;

// ==========================================
// Queries
// ==========================================

/// <summary>Query to retrieve organization ERP sales report.</summary>
public sealed record GetSalesReportQuery(
    Guid OrganizationId,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    Guid? CustomerId = null,
    Currency? Currency = null) : IRequest<SalesReportDto>;

/// <summary>Query to retrieve organization ERP purchase report.</summary>
public sealed record GetPurchaseReportQuery(
    Guid OrganizationId,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    Guid? SupplierId = null,
    Currency? Currency = null) : IRequest<PurchaseReportDto>;

/// <summary>Query to retrieve organization ERP settlement report.</summary>
public sealed record GetSettlementReportQuery(
    Guid OrganizationId,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    Currency? Currency = null,
    string? SettlementMethod = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<SettlementReportDto>;

/// <summary>Query to retrieve organization ERP Profit &amp; Loss report.</summary>
public sealed record GetProfitLossReportQuery(
    Guid OrganizationId,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    Currency? Currency = null) : IRequest<ProfitLossReportDto>;

// ==========================================
// Query Handlers
// ==========================================

/// <summary>Handler for <see cref="GetSalesReportQuery"/>.</summary>
public sealed class GetSalesReportQueryHandler : IRequestHandler<GetSalesReportQuery, SalesReportDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    public GetSalesReportQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    public async Task<SalesReportDto> Handle(GetSalesReportQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var ordersQuery = _dbContext.SalesOrders
            .Where(s => s.OrganizationId == request.OrganizationId);

        if (request.FromUtc.HasValue)
        {
            ordersQuery = ordersQuery.Where(s => s.OrderDate >= request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            ordersQuery = ordersQuery.Where(s => s.OrderDate <= request.ToUtc.Value);
        }

        if (request.CustomerId.HasValue)
        {
            ordersQuery = ordersQuery.Where(s => s.CustomerId == request.CustomerId.Value);
        }

        if (request.Currency.HasValue)
        {
            ordersQuery = ordersQuery.Where(s => s.Currency == request.Currency.Value);
        }

        var orders = await ordersQuery.ToListAsync(cancellationToken);
        var orderIds = orders.Select(o => o.Id).ToList();

        var orderItems = await _dbContext.SalesOrderItems
            .Where(i => orderIds.Contains(i.SalesOrderId))
            .ToListAsync(cancellationToken);

        // Fetch customers for name resolution
        var customerIds = orders.Select(o => o.CustomerId).Distinct().ToList();
        var customers = await _dbContext.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToListAsync(cancellationToken);
        var customerMap = customers.ToDictionary(c => c.Id, c => c.Name);

        // Status counts
        var totalOrders = orders.Count;
        var draftOrders = orders.Count(o => o.Status == SalesOrderStatus.Draft);
        var confirmedOrders = orders.Count(o => o.Status == SalesOrderStatus.Confirmed);
        var partiallyFulfilledOrders = orders.Count(o => o.Status == SalesOrderStatus.PartiallyFulfilled);
        var fulfilledOrders = orders.Count(o => o.Status == SalesOrderStatus.Fulfilled);
        var cancelledOrders = orders.Count(o => o.Status == SalesOrderStatus.Cancelled);

        // Currency summaries (excluding Cancelled)
        var activeOrders = orders.Where(o => o.Status != SalesOrderStatus.Cancelled).ToList();
        var currencySummaries = activeOrders
            .GroupBy(o => o.Currency)
            .Select(g => new CurrencySalesSummaryDto(
                Currency: g.Key,
                TotalGrossSales: g.Sum(o => o.TotalAmount),
                TotalVatAmount: 0m,
                TotalNetSales: g.Sum(o => o.TotalAmount),
                OrderCount: g.Count()))
            .ToList();

        // Top Customers
        var topCustomers = activeOrders
            .GroupBy(o => new { o.CustomerId, o.Currency })
            .Select(g => new CustomerSalesSummaryDto(
                CustomerId: g.Key.CustomerId,
                CustomerName: customerMap.GetValueOrDefault(g.Key.CustomerId, "Unknown Customer"),
                OrderCount: g.Count(),
                TotalAmount: g.Sum(o => o.TotalAmount),
                Currency: g.Key.Currency))
            .OrderByDescending(c => c.TotalAmount)
            .Take(10)
            .ToList();

        // Item Sales
        var itemSales = orderItems
            .GroupBy(i => new { i.Description })
            .Select(g =>
            {
                var sampleOrder = orders.FirstOrDefault(o => o.Id == g.First().SalesOrderId);
                var cur = sampleOrder?.Currency ?? Currency.NGN;
                return new ItemSalesSummaryDto(
                    ItemName: g.Key.Description,
                    QuantitySold: g.Sum(i => i.FulfilledQuantity > 0 ? i.FulfilledQuantity : i.Quantity),
                    TotalRevenue: g.Sum(i => i.TotalAmount),
                    Currency: cur);
            })
            .OrderByDescending(i => i.TotalRevenue)
            .Take(20)
            .ToList();

        return new SalesReportDto(
            request.OrganizationId,
            request.FromUtc,
            request.ToUtc,
            totalOrders,
            draftOrders,
            confirmedOrders,
            partiallyFulfilledOrders,
            fulfilledOrders,
            cancelledOrders,
            currencySummaries,
            topCustomers,
            itemSales);
    }
}

/// <summary>Handler for <see cref="GetPurchaseReportQuery"/>.</summary>
public sealed class GetPurchaseReportQueryHandler : IRequestHandler<GetPurchaseReportQuery, PurchaseReportDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    public GetPurchaseReportQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    public async Task<PurchaseReportDto> Handle(GetPurchaseReportQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var ordersQuery = _dbContext.PurchaseOrders
            .Where(p => p.OrganizationId == request.OrganizationId);

        if (request.FromUtc.HasValue)
        {
            ordersQuery = ordersQuery.Where(p => p.OrderDate >= request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            ordersQuery = ordersQuery.Where(p => p.OrderDate <= request.ToUtc.Value);
        }

        if (request.SupplierId.HasValue)
        {
            ordersQuery = ordersQuery.Where(p => p.SupplierId == request.SupplierId.Value);
        }

        if (request.Currency.HasValue)
        {
            ordersQuery = ordersQuery.Where(p => p.Currency == request.Currency.Value);
        }

        var orders = await ordersQuery.ToListAsync(cancellationToken);
        var orderIds = orders.Select(o => o.Id).ToList();

        var orderItems = await _dbContext.PurchaseOrderItems
            .Where(i => orderIds.Contains(i.PurchaseOrderId))
            .ToListAsync(cancellationToken);

        // Fetch suppliers for name resolution
        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var suppliers = await _dbContext.Suppliers
            .Where(s => supplierIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
        var supplierMap = suppliers.ToDictionary(s => s.Id, s => s.Name);

        // Status counts
        var totalOrders = orders.Count;
        var draftOrders = orders.Count(o => o.Status == PurchaseOrderStatus.Draft);
        var confirmedOrders = orders.Count(o => o.Status == PurchaseOrderStatus.Confirmed);
        var partiallyReceivedOrders = orders.Count(o => o.Status == PurchaseOrderStatus.PartiallyReceived);
        var receivedOrders = orders.Count(o => o.Status == PurchaseOrderStatus.Received);
        var cancelledOrders = orders.Count(o => o.Status == PurchaseOrderStatus.Cancelled);

        // Currency summaries (excluding Cancelled)
        var activeOrders = orders.Where(o => o.Status != PurchaseOrderStatus.Cancelled).ToList();
        var currencySummaries = activeOrders
            .GroupBy(o => o.Currency)
            .Select(g => new CurrencyPurchaseSummaryDto(
                Currency: g.Key,
                TotalPurchasesAmount: g.Sum(o => o.TotalAmount),
                OrderCount: g.Count()))
            .ToList();

        // Top Suppliers
        var topSuppliers = activeOrders
            .GroupBy(o => new { o.SupplierId, o.Currency })
            .Select(g => new SupplierPurchaseSummaryDto(
                SupplierId: g.Key.SupplierId,
                SupplierName: supplierMap.GetValueOrDefault(g.Key.SupplierId, "Unknown Supplier"),
                OrderCount: g.Count(),
                TotalAmount: g.Sum(o => o.TotalAmount),
                Currency: g.Key.Currency))
            .OrderByDescending(s => s.TotalAmount)
            .Take(10)
            .ToList();

        // Item Purchases
        var itemPurchases = orderItems
            .GroupBy(i => new { i.Description })
            .Select(g =>
            {
                var sampleOrder = orders.FirstOrDefault(o => o.Id == g.First().PurchaseOrderId);
                var cur = sampleOrder?.Currency ?? Currency.NGN;
                return new ItemPurchaseSummaryDto(
                    ItemName: g.Key.Description,
                    QuantityOrdered: g.Sum(i => i.Quantity),
                    QuantityReceived: g.Sum(i => i.ReceivedQuantity),
                    TotalCost: g.Sum(i => i.TotalAmount),
                    Currency: cur);
            })
            .OrderByDescending(i => i.TotalCost)
            .Take(20)
            .ToList();

        return new PurchaseReportDto(
            request.OrganizationId,
            request.FromUtc,
            request.ToUtc,
            totalOrders,
            draftOrders,
            confirmedOrders,
            partiallyReceivedOrders,
            receivedOrders,
            cancelledOrders,
            currencySummaries,
            topSuppliers,
            itemPurchases);
    }
}

/// <summary>Handler for <see cref="GetSettlementReportQuery"/>.</summary>
public sealed class GetSettlementReportQueryHandler : IRequestHandler<GetSettlementReportQuery, SettlementReportDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    public GetSettlementReportQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    public async Task<SettlementReportDto> Handle(GetSettlementReportQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var allSettlements = new List<SettlementItemDto>();

        // 1. Invoices paid / partially paid
        var invoicesQuery = _dbContext.ErpInvoices
            .Where(i => i.OrganizationId == request.OrganizationId && i.PaidAmount > 0);

        if (request.FromUtc.HasValue) invoicesQuery = invoicesQuery.Where(i => (i.UpdatedAtUtc ?? i.IssueDate) >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) invoicesQuery = invoicesQuery.Where(i => (i.UpdatedAtUtc ?? i.IssueDate) <= request.ToUtc.Value);
        if (request.Currency.HasValue) invoicesQuery = invoicesQuery.Where(i => i.Currency == request.Currency.Value);

        var invoices = await invoicesQuery.ToListAsync(cancellationToken);
        var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();
        var customers = await _dbContext.Customers.Where(c => customerIds.Contains(c.Id)).ToListAsync(cancellationToken);
        var customerMap = customers.ToDictionary(c => c.Id, c => c.Name);

        foreach (var inv in invoices)
        {
            var party = customerMap.GetValueOrDefault(inv.CustomerId, "Customer");
            allSettlements.Add(new SettlementItemDto(
                SettlementType: "InvoicePayment",
                DocumentId: inv.Id,
                DocumentNumber: inv.InvoiceNumber,
                SettlementMethod: inv.SettlementMethod.ToString(),
                Amount: inv.PaidAmount,
                Currency: inv.Currency,
                SettlementDateUtc: inv.UpdatedAtUtc ?? inv.IssueDate,
                LedgerTransactionId: null,
                Reference: inv.InvoiceNumber,
                PartyName: party));
        }

        // 2. Operating Expenses paid
        var expensesQuery = _dbContext.OperatingExpenses
            .Where(e => e.OrganizationId == request.OrganizationId && e.Status == ExpenseStatus.Paid);

        if (request.FromUtc.HasValue) expensesQuery = expensesQuery.Where(e => e.PaidAtUtc >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) expensesQuery = expensesQuery.Where(e => e.PaidAtUtc <= request.ToUtc.Value);
        if (request.Currency.HasValue) expensesQuery = expensesQuery.Where(e => e.Currency == request.Currency.Value);

        var expenses = await expensesQuery.ToListAsync(cancellationToken);
        foreach (var exp in expenses)
        {
            allSettlements.Add(new SettlementItemDto(
                SettlementType: "OperatingExpense",
                DocumentId: exp.Id,
                DocumentNumber: exp.ExpenseNumber,
                SettlementMethod: exp.PaymentMethod.ToString(),
                Amount: exp.Amount,
                Currency: exp.Currency,
                SettlementDateUtc: exp.PaidAtUtc ?? exp.CreatedAtUtc,
                LedgerTransactionId: exp.LedgerTransactionId,
                Reference: exp.Reference,
                PartyName: exp.Category.ToString()));
        }

        // 3. Company Vouchers paid
        var vouchersQuery = _dbContext.CompanyVouchers
            .Where(v => v.OrganizationId == request.OrganizationId && v.Status == CompanyVoucherStatus.Paid);

        if (request.FromUtc.HasValue) vouchersQuery = vouchersQuery.Where(v => v.PaidAtUtc >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) vouchersQuery = vouchersQuery.Where(v => v.PaidAtUtc <= request.ToUtc.Value);
        if (request.Currency.HasValue) vouchersQuery = vouchersQuery.Where(v => v.Currency == request.Currency.Value);

        var vouchers = await vouchersQuery.ToListAsync(cancellationToken);
        foreach (var v in vouchers)
        {
            allSettlements.Add(new SettlementItemDto(
                SettlementType: "CompanyVoucher",
                DocumentId: v.Id,
                DocumentNumber: v.VoucherNumber,
                SettlementMethod: v.PaymentMethod.ToString(),
                Amount: v.Amount,
                Currency: v.Currency,
                SettlementDateUtc: v.PaidAtUtc ?? v.CreatedAtUtc,
                LedgerTransactionId: v.LedgerTransactionId,
                Reference: v.Reference,
                PartyName: v.PayeeName));
        }

        // Optional filter by SettlementMethod string (Wallet vs Manual)
        if (!string.IsNullOrWhiteSpace(request.SettlementMethod))
        {
            var filterMethod = request.SettlementMethod.Trim().ToLowerInvariant();
            allSettlements = allSettlements
                .Where(s => s.SettlementMethod.ToLowerInvariant() == filterMethod)
                .ToList();
        }

        // Currency summaries
        var currencySummaries = allSettlements
            .GroupBy(s => s.Currency)
            .Select(g => new CurrencySettlementSummaryDto(
                Currency: g.Key,
                TotalWalletSettlements: g.Where(s => s.SettlementMethod == "Wallet").Sum(s => s.Amount),
                TotalManualSettlements: g.Where(s => s.SettlementMethod == "Manual").Sum(s => s.Amount),
                GrandTotal: g.Sum(s => s.Amount),
                SettlementCount: g.Count()))
            .ToList();

        // Paging
        var totalCount = allSettlements.Count;
        var pagedItems = allSettlements
            .OrderByDescending(s => s.SettlementDateUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var pagedResult = new PagedResult<SettlementItemDto>(pagedItems, totalCount, request.PageNumber, request.PageSize);

        return new SettlementReportDto(
            request.OrganizationId,
            request.FromUtc,
            request.ToUtc,
            currencySummaries,
            pagedResult);
    }
}

/// <summary>Handler for <see cref="GetProfitLossReportQuery"/>.</summary>
public sealed class GetProfitLossReportQueryHandler : IRequestHandler<GetProfitLossReportQuery, ProfitLossReportDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    public GetProfitLossReportQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    public async Task<ProfitLossReportDto> Handle(GetProfitLossReportQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        // 1. Revenue: Invoices issued/paid + sales orders
        var invoicesQuery = _dbContext.ErpInvoices
            .Where(i => i.OrganizationId == request.OrganizationId && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft);

        if (request.FromUtc.HasValue) invoicesQuery = invoicesQuery.Where(i => i.IssueDate >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) invoicesQuery = invoicesQuery.Where(i => i.IssueDate <= request.ToUtc.Value);
        if (request.Currency.HasValue) invoicesQuery = invoicesQuery.Where(i => i.Currency == request.Currency.Value);

        var invoices = await invoicesQuery.ToListAsync(cancellationToken);

        // Also check standalone fulfilled sales orders that aren't invoiced
        var soQuery = _dbContext.SalesOrders
            .Where(s => s.OrganizationId == request.OrganizationId && s.Status == SalesOrderStatus.Fulfilled);

        if (request.FromUtc.HasValue) soQuery = soQuery.Where(s => s.OrderDate >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) soQuery = soQuery.Where(s => s.OrderDate <= request.ToUtc.Value);
        if (request.Currency.HasValue) soQuery = soQuery.Where(s => s.Currency == request.Currency.Value);

        var salesOrders = await soQuery.ToListAsync(cancellationToken);

        // 2. Cost of Goods Sold (COGS): Authoritative Phase 5C StockMovements (StockOut)
        var cogsQuery = _dbContext.StockMovements
            .Where(m => m.OrganizationId == request.OrganizationId && m.MovementType == StockMovementType.StockOut);

        if (request.FromUtc.HasValue) cogsQuery = cogsQuery.Where(m => m.CreatedAtUtc >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) cogsQuery = cogsQuery.Where(m => m.CreatedAtUtc <= request.ToUtc.Value);

        var stockOutMovements = await cogsQuery.ToListAsync(cancellationToken);

        // 3. Operating Expenses: Paid OperatingExpense records
        var expenseQuery = _dbContext.OperatingExpenses
            .Where(e => e.OrganizationId == request.OrganizationId && e.Status == ExpenseStatus.Paid);

        if (request.FromUtc.HasValue) expenseQuery = expenseQuery.Where(e => (e.PaidAtUtc ?? e.ExpenseDate) >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) expenseQuery = expenseQuery.Where(e => (e.PaidAtUtc ?? e.ExpenseDate) <= request.ToUtc.Value);
        if (request.Currency.HasValue) expenseQuery = expenseQuery.Where(e => e.Currency == request.Currency.Value);

        var expenses = await expenseQuery.ToListAsync(cancellationToken);

        // 4. Company Voucher Disbursements: Paid CompanyVoucher records
        var voucherQuery = _dbContext.CompanyVouchers
            .Where(v => v.OrganizationId == request.OrganizationId && v.Status == CompanyVoucherStatus.Paid);

        if (request.FromUtc.HasValue) voucherQuery = voucherQuery.Where(v => (v.PaidAtUtc ?? v.CreatedAtUtc) >= request.FromUtc.Value);
        if (request.ToUtc.HasValue) voucherQuery = voucherQuery.Where(v => (v.PaidAtUtc ?? v.CreatedAtUtc) <= request.ToUtc.Value);
        if (request.Currency.HasValue) voucherQuery = voucherQuery.Where(v => v.Currency == request.Currency.Value);

        var vouchers = await voucherQuery.ToListAsync(cancellationToken);

        // Collect all active currencies
        var activeCurrencies = invoices.Select(i => i.Currency)
            .Concat(salesOrders.Select(s => s.Currency))
            .Concat(expenses.Select(e => e.Currency))
            .Concat(vouchers.Select(v => v.Currency))
            .Distinct()
            .ToList();

        if (activeCurrencies.Count == 0)
        {
            activeCurrencies.Add(request.Currency ?? Currency.NGN);
        }

        var summaries = new List<CurrencyProfitLossSummaryDto>();

        foreach (var cur in activeCurrencies)
        {
            // Revenue for this currency: Prioritize invoices; if no invoices use sales orders
            var curInvoices = invoices.Where(i => i.Currency == cur).ToList();
            var curSalesOrders = salesOrders.Where(s => s.Currency == cur).ToList();

            decimal totalRevenue;
            if (curInvoices.Count > 0)
            {
                totalRevenue = curInvoices.Sum(i => i.Subtotal); // Revenue before tax
            }
            else
            {
                totalRevenue = curSalesOrders.Sum(s => s.TotalAmount);
            }

            // COGS for this currency (StockOut total cost is in organization base currency NGN)
            decimal totalCogs = 0m;
            if (cur == Currency.NGN)
            {
                totalCogs = stockOutMovements.Sum(m => m.TotalCost ?? 0m);
            }

            var grossProfit = totalRevenue - totalCogs;

            // Operating expenses for this currency
            var curExpenses = expenses.Where(e => e.Currency == cur).ToList();
            var totalOperatingExpenses = curExpenses.Sum(e => e.Amount);

            var expenseBreakdown = curExpenses
                .GroupBy(e => e.Category)
                .Select(g => new ExpenseCategoryBreakdownDto(g.Key.ToString(), g.Sum(e => e.Amount)))
                .OrderByDescending(b => b.Amount)
                .ToList();

            // Company vouchers for this currency
            var curVouchers = vouchers.Where(v => v.Currency == cur).ToList();
            var totalVoucherDisbursements = curVouchers.Sum(v => v.Amount);

            var totalExpenses = totalOperatingExpenses + totalVoucherDisbursements;
            var netProfitLoss = grossProfit - totalExpenses;

            summaries.Add(new CurrencyProfitLossSummaryDto(
                Currency: cur,
                TotalRevenue: totalRevenue,
                TotalCogs: totalCogs,
                GrossProfit: grossProfit,
                OperatingExpenses: totalOperatingExpenses,
                CompanyVoucherDisbursements: totalVoucherDisbursements,
                TotalExpenses: totalExpenses,
                NetProfitLoss: netProfitLoss,
                ExpenseBreakdown: expenseBreakdown));
        }

        return new ProfitLossReportDto(
            request.OrganizationId,
            request.FromUtc,
            request.ToUtc,
            summaries);
    }
}
