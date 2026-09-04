using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
using CebizPay.Application.Common.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Erp.Entities;
using CebizPay.Domain.Erp.Enums;
using CebizPay.Domain.Erp.Events;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Erp;

#pragma warning disable CS1591, CA1862, CA1304, CA1311

// ==========================================
// Commands & Queries
// ==========================================

/// <summary>Command to create an ERP invoice.</summary>
public sealed record CreateInvoiceCommand(
    Guid OrganizationId,
    Guid CustomerId,
    DateTime IssueDate,
    DateTime DueDate,
    bool ApplyVat,
    Guid? SalesOrderId,
    Currency Currency,
    string? Notes,
    string? BillingContact,
    List<InvoiceItemRequest> Items) : IRequest<Guid>;

/// <summary>Command to issue a draft invoice.</summary>
public sealed record IssueInvoiceCommand(
    Guid OrganizationId,
    Guid InvoiceId) : IRequest<Unit>;

/// <summary>Command to record a payment towards an invoice.</summary>
public sealed record RecordInvoicePaymentCommand(
    Guid OrganizationId,
    Guid InvoiceId,
    decimal Amount,
    InvoiceSettlementMethod SettlementMethod,
    string Reference,
    string? Pin = null,
    string? IdempotencyKey = null) : IRequest<Guid?>;

/// <summary>Command to cancel an invoice.</summary>
public sealed record CancelInvoiceCommand(
    Guid OrganizationId,
    Guid InvoiceId) : IRequest<Unit>;

/// <summary>Query to retrieve paged invoices.</summary>
public sealed record GetInvoicesQuery(
    Guid OrganizationId,
    InvoiceStatus? Status = null,
    Guid? CustomerId = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<ErpInvoiceDto>>;

/// <summary>Query to retrieve invoice details by ID.</summary>
public sealed record GetInvoiceByIdQuery(
    Guid OrganizationId,
    Guid InvoiceId) : IRequest<ErpInvoiceDto>;

// ==========================================
// Handlers
// ==========================================

/// <summary>Handler for <see cref="CreateInvoiceCommand"/>.</summary>
public sealed class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxService _outbox;

    public CreateInvoiceCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUser,
        IOutboxService outbox)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _currentUser = currentUser;
        _outbox = outbox;
    }

    public async Task<Guid> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization '{request.OrganizationId}' not found.");

        if (org.Status == OrganizationStatus.Suspended)
        {
            throw new InvalidOperationException("Operation not permitted. The organization is suspended.");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            throw new ArgumentException("Invoice must contain at least one line item.", nameof(request));
        }

        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.OrganizationId == request.OrganizationId && !c.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException($"Customer '{request.CustomerId}' not found.");

        var userId = _currentUser.UserId ?? "system";
        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..18].ToUpperInvariant();

        var invoice = new ErpInvoice(
            request.OrganizationId,
            invoiceNumber,
            customer.Id,
            userId,
            request.IssueDate,
            request.DueDate,
            request.ApplyVat,
            request.SalesOrderId,
            request.Currency,
            request.Notes,
            request.BillingContact);

        foreach (var item in request.Items)
        {
            invoice.AddItem(item.Description, item.Quantity, item.UnitPrice, item.InventoryItemId, item.ServiceId);
        }

        _dbContext.ErpInvoices.Add(invoice);

        var auditLog = AuditLog.Create(
            userId,
            AuditActions.InvoiceCreated,
            AuditResourceTypes.Invoice,
            invoice.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Created invoice '{invoice.InvoiceNumber}' for customer '{customer.Name}' with total {invoice.TotalAmount} {invoice.Currency} (VAT: {invoice.VatAmount}).");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new InvoiceCreatedDomainEvent(
            invoice.Id,
            invoice.OrganizationId,
            invoice.InvoiceNumber,
            invoice.CustomerId,
            invoice.TotalAmount,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return invoice.Id;
    }
}

/// <summary>Handler for <see cref="IssueInvoiceCommand"/>.</summary>
public sealed class IssueInvoiceCommandHandler : IRequestHandler<IssueInvoiceCommand, Unit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxService _outbox;

    public IssueInvoiceCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUser,
        IOutboxService outbox)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _currentUser = currentUser;
        _outbox = outbox;
    }

    public async Task<Unit> Handle(IssueInvoiceCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization '{request.OrganizationId}' not found.");

        if (org.Status == OrganizationStatus.Suspended)
        {
            throw new InvalidOperationException("Operation not permitted. The organization is suspended.");
        }

        var invoice = await _dbContext.ErpInvoices
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId && i.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Invoice '{request.InvoiceId}' not found.");

        var now = DateTime.UtcNow;
        invoice.Issue(now);

        var userId = _currentUser.UserId ?? "system";
        var auditLog = AuditLog.Create(
            userId,
            AuditActions.InvoiceIssued,
            AuditResourceTypes.Invoice,
            invoice.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Issued invoice '{invoice.InvoiceNumber}' due on {invoice.DueDate:yyyy-MM-dd}.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new InvoiceIssuedDomainEvent(
            invoice.Id,
            invoice.OrganizationId,
            invoice.InvoiceNumber,
            invoice.DueDate,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Handler for <see cref="RecordInvoicePaymentCommand"/>.</summary>
public sealed class RecordInvoicePaymentCommandHandler : IRequestHandler<RecordInvoicePaymentCommand, Guid?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ITransactionPinService _pinService;
    private readonly ILedgerPostingService _ledgerService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IOutboxService _outbox;

    public RecordInvoicePaymentCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUser,
        ITransactionPinService pinService,
        ILedgerPostingService ledgerService,
        IIdempotencyService idempotencyService,
        IOutboxService outbox)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _currentUser = currentUser;
        _pinService = pinService;
        _ledgerService = ledgerService;
        _idempotencyService = idempotencyService;
        _outbox = outbox;
    }

    public async Task<Guid?> Handle(RecordInvoicePaymentCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization '{request.OrganizationId}' not found.");

        if (org.Status == OrganizationStatus.Suspended)
        {
            throw new InvalidOperationException("Operation not permitted. The organization is suspended.");
        }

        var invoice = await _dbContext.ErpInvoices
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId && i.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Invoice '{request.InvoiceId}' not found.");

        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User authentication context is required.");

        var now = DateTime.UtcNow;

        await using var tx = await _dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            if (request.SettlementMethod == InvoiceSettlementMethod.Wallet)
        {
            if (string.IsNullOrWhiteSpace(request.Pin))
            {
                throw new ArgumentException("Transaction PIN is required for wallet payments.", nameof(request));
            }

            var (pinSuccess, isLocked, pinError) = await _pinService.VerifyPinAsync(userId, request.Pin, cancellationToken);
            if (!pinSuccess)
            {
                if (isLocked)
                {
                    throw new InvalidOperationException("Account PIN is temporarily locked due to too many failed attempts.");
                }
                throw new InvalidOperationException(pinError ?? "Invalid transaction PIN.");
            }

            // Check Idempotency if provided
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var existingRecord = await _idempotencyService.GetRecordAsync(
                    request.IdempotencyKey,
                    "RecordInvoicePayment",
                    userId,
                    request.OrganizationId,
                    cancellationToken);

                if (existingRecord != null && existingRecord.Status == IdempotencyStatus.Completed)
                {
                    return null;
                }
            }

            // Payer wallet (user's individual or organization wallet)
            var userWallet = await _dbContext.Wallets
                .FirstOrDefaultAsync(w => w.IndividualId == userId && w.Currency == invoice.Currency, cancellationToken)
                ?? await _dbContext.Wallets
                    .FirstOrDefaultAsync(w => w.OrganizationId == request.OrganizationId && w.Currency == invoice.Currency, cancellationToken)
                ?? throw new InvalidOperationException($"No active wallet found for currency '{invoice.Currency}'.");

            if (userWallet.AvailableBalance < request.Amount)
            {
                throw new InsufficientFundsException(userWallet.AvailableBalance, request.Amount);
            }

            // Target organization wallet
            var orgWallet = await _dbContext.Wallets
                .FirstOrDefaultAsync(w => w.OrganizationId == request.OrganizationId && w.Currency == invoice.Currency, cancellationToken)
                ?? throw new InvalidOperationException($"No wallet found for organization '{request.OrganizationId}' in currency '{invoice.Currency}'.");

            var sourceLedgerAccount = await _dbContext.LedgerAccounts
                .FirstOrDefaultAsync(l => l.WalletId == userWallet.Id, cancellationToken)
                ?? throw new InvalidOperationException("Source ledger account for payer wallet not found.");

            var targetLedgerAccount = await _dbContext.LedgerAccounts
                .FirstOrDefaultAsync(l => l.WalletId == orgWallet.Id, cancellationToken)
                ?? throw new InvalidOperationException("Target ledger account for organization wallet not found.");

            // Post double entry transaction: User Wallet -> Org Wallet
            await _ledgerService.PostSingleCurrencyTransactionAsync(
                sourceLedgerAccount.Id,
                targetLedgerAccount.Id,
                request.Amount,
                invoice.Currency,
                LedgerTransactionType.ErpInvoicePayment,
                reference: invoice.InvoiceNumber,
                idempotencyKey: request.IdempotencyKey,
                description: $"Payment for invoice {invoice.InvoiceNumber}",
                cancellationToken: cancellationToken);

            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var record = await _idempotencyService.CreateRecordAsync(
                    request.IdempotencyKey,
                    "RecordInvoicePayment",
                    $"{invoice.Id}:{request.Amount}",
                    userId,
                    request.OrganizationId,
                    autoSave: false,
                    cancellationToken: cancellationToken);
                await _idempotencyService.CompleteRecordAsync(record.Id, "{\"status\":\"success\"}", cancellationToken);
            }
        }

        invoice.RecordPayment(request.Amount, request.SettlementMethod, now);

        var auditLog = AuditLog.Create(
            userId,
            AuditActions.InvoicePaid,
            AuditResourceTypes.Invoice,
            invoice.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Recorded payment of {request.Amount} {invoice.Currency} on invoice '{invoice.InvoiceNumber}' via {request.SettlementMethod}. Status is now '{invoice.Status}'.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new InvoicePaidDomainEvent(
            invoice.Id,
            invoice.OrganizationId,
            invoice.InvoiceNumber,
            request.Amount,
            request.SettlementMethod,
            now));

        Guid? receiptId = null;

        // Atomically generate receipt exactly once when invoice reaches Paid status
        if (invoice.Status == InvoiceStatus.Paid)
        {
            var existingReceipt = await _dbContext.ErpReceipts
                .FirstOrDefaultAsync(r => r.InvoiceId == invoice.Id, cancellationToken);

            if (existingReceipt == null)
            {
                var receiptNumber = $"REC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
                var receipt = new ErpReceipt(
                    request.OrganizationId,
                    receiptNumber,
                    invoice.Id,
                    invoice.CustomerId,
                    invoice.TotalAmount,
                    now,
                    request.SettlementMethod,
                    request.Reference,
                    userId,
                    invoice.Currency,
                    invoice.Notes);

                _dbContext.ErpReceipts.Add(receipt);
                receiptId = receipt.Id;

                var receiptAuditLog = AuditLog.Create(
                    userId,
                    AuditActions.ReceiptGenerated,
                    AuditResourceTypes.Receipt,
                    receipt.Id.ToString(),
                    request.OrganizationId,
                    afterJson: $"Generated receipt '{receipt.ReceiptNumber}' for invoice '{invoice.InvoiceNumber}' for amount {receipt.Amount} {receipt.Currency}.");
                _dbContext.AuditLogs.Add(receiptAuditLog);

                _outbox.Write(new ReceiptGeneratedDomainEvent(
                    receipt.Id,
                    receipt.OrganizationId,
                    receipt.ReceiptNumber,
                    receipt.InvoiceId,
                    receipt.Amount,
                    now));
            }
            else
            {
                receiptId = existingReceipt.Id;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return receiptId;
    }
    catch
    {
        await tx.RollbackAsync(cancellationToken);
        throw;
    }
}
}

/// <summary>Handler for <see cref="CancelInvoiceCommand"/>.</summary>
public sealed class CancelInvoiceCommandHandler : IRequestHandler<CancelInvoiceCommand, Unit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxService _outbox;

    public CancelInvoiceCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUser,
        IOutboxService outbox)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _currentUser = currentUser;
        _outbox = outbox;
    }

    public async Task<Unit> Handle(CancelInvoiceCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var invoice = await _dbContext.ErpInvoices
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId && i.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Invoice '{request.InvoiceId}' not found.");

        var userId = _currentUser.UserId ?? "system";
        var now = DateTime.UtcNow;

        invoice.Cancel(now);

        var auditLog = AuditLog.Create(
            userId,
            AuditActions.InvoiceCancelled,
            AuditResourceTypes.Invoice,
            invoice.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Cancelled invoice '{invoice.InvoiceNumber}'.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new InvoiceCancelledDomainEvent(
            invoice.Id,
            invoice.OrganizationId,
            invoice.InvoiceNumber,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Handler for <see cref="GetInvoicesQuery"/>.</summary>
public sealed class GetInvoicesQueryHandler : IRequestHandler<GetInvoicesQuery, PagedResult<ErpInvoiceDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    public GetInvoicesQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    public async Task<PagedResult<ErpInvoiceDto>> Handle(GetInvoicesQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var query = _dbContext.ErpInvoices
            .Where(i => i.OrganizationId == request.OrganizationId);

        if (request.Status.HasValue)
        {
            query = query.Where(i => i.Status == request.Status.Value);
        }

        if (request.CustomerId.HasValue)
        {
            query = query.Where(i => i.CustomerId == request.CustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(i => i.InvoiceNumber.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var invoices = await query
            .OrderByDescending(i => i.IssueDate)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var invoiceIds = invoices.Select(i => i.Id).ToList();
        var allItems = await _dbContext.ErpInvoiceItems
            .Where(i => invoiceIds.Contains(i.ErpInvoiceId))
            .ToListAsync(cancellationToken);

        var itemsByInvoice = allItems.GroupBy(i => i.ErpInvoiceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var dtos = invoices.Select(i => new ErpInvoiceDto(
            i.Id,
            i.OrganizationId,
            i.InvoiceNumber,
            i.CustomerId,
            i.SalesOrderId,
            i.IssueDate,
            i.DueDate,
            i.ApplyVat,
            i.VatRate,
            i.Subtotal,
            i.VatAmount,
            i.TotalAmount,
            i.PaidAmount,
            i.Currency,
            i.Status,
            i.SettlementMethod,
            i.Notes,
            i.BillingContact,
            i.CreatedByUserId,
            i.CreatedAtUtc,
            i.UpdatedAtUtc,
            (itemsByInvoice.TryGetValue(i.Id, out var lines) ? lines : new List<ErpInvoiceItem>())
                .Select(item => new ErpInvoiceItemDto(
                    item.Id,
                    item.ErpInvoiceId,
                    item.InventoryItemId,
                    item.ServiceId,
                    item.Description,
                    item.Quantity,
                    item.UnitPrice,
                    item.TotalAmount)).ToList())).ToList();

        return new PagedResult<ErpInvoiceDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}

/// <summary>Handler for <see cref="GetInvoiceByIdQuery"/>.</summary>
public sealed class GetInvoiceByIdQueryHandler : IRequestHandler<GetInvoiceByIdQuery, ErpInvoiceDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    public GetInvoiceByIdQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    public async Task<ErpInvoiceDto> Handle(GetInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var i = await _dbContext.ErpInvoices
            .FirstOrDefaultAsync(inv => inv.Id == request.InvoiceId && inv.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Invoice '{request.InvoiceId}' not found.");

        var lines = await _dbContext.ErpInvoiceItems
            .Where(item => item.ErpInvoiceId == i.Id)
            .ToListAsync(cancellationToken);

        return new ErpInvoiceDto(
            i.Id,
            i.OrganizationId,
            i.InvoiceNumber,
            i.CustomerId,
            i.SalesOrderId,
            i.IssueDate,
            i.DueDate,
            i.ApplyVat,
            i.VatRate,
            i.Subtotal,
            i.VatAmount,
            i.TotalAmount,
            i.PaidAmount,
            i.Currency,
            i.Status,
            i.SettlementMethod,
            i.Notes,
            i.BillingContact,
            i.CreatedByUserId,
            i.CreatedAtUtc,
            i.UpdatedAtUtc,
            lines.Select(item => new ErpInvoiceItemDto(
                item.Id,
                item.ErpInvoiceId,
                item.InventoryItemId,
                item.ServiceId,
                item.Description,
                item.Quantity,
                item.UnitPrice,
                item.TotalAmount)).ToList());
    }
}

#pragma warning restore CA1862, CA1304, CA1311
