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

/// <summary>Command to create an operating expense.</summary>
public sealed record CreateOperatingExpenseCommand(
    Guid OrganizationId,
    ExpenseCategory Category,
    string Description,
    decimal Amount,
    DateTime ExpenseDate,
    ExpensePaymentMethod PaymentMethod = ExpensePaymentMethod.Manual,
    Guid? SupplierId = null,
    Currency Currency = Currency.NGN,
    string? Reference = null) : IRequest<Guid>;

/// <summary>Command to approve an operating expense.</summary>
public sealed record ApproveOperatingExpenseCommand(
    Guid OrganizationId,
    Guid ExpenseId) : IRequest<Unit>;

/// <summary>Command to pay an operating expense.</summary>
public sealed record PayOperatingExpenseCommand(
    Guid OrganizationId,
    Guid ExpenseId,
    ExpensePaymentMethod PaymentMethod,
    string? Pin = null,
    string? IdempotencyKey = null,
    string? Reference = null) : IRequest<Unit>;

/// <summary>Command to cancel an operating expense.</summary>
public sealed record CancelOperatingExpenseCommand(
    Guid OrganizationId,
    Guid ExpenseId) : IRequest<Unit>;

/// <summary>Query to retrieve paged operating expenses.</summary>
public sealed record GetOperatingExpensesQuery(
    Guid OrganizationId,
    ExpenseCategory? Category = null,
    ExpenseStatus? Status = null,
    Guid? SupplierId = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<OperatingExpenseDto>>;

/// <summary>Query to retrieve operating expense details by ID.</summary>
public sealed record GetOperatingExpenseByIdQuery(
    Guid OrganizationId,
    Guid ExpenseId) : IRequest<OperatingExpenseDto>;

// ==========================================
// Handlers
// ==========================================

/// <summary>Handler for <see cref="CreateOperatingExpenseCommand"/>.</summary>
public sealed class CreateOperatingExpenseCommandHandler : IRequestHandler<CreateOperatingExpenseCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxService _outbox;

    public CreateOperatingExpenseCommandHandler(
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

    public async Task<Guid> Handle(CreateOperatingExpenseCommand request, CancellationToken cancellationToken)
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

        if (request.SupplierId.HasValue)
        {
            var supplierExists = await _dbContext.Suppliers
                .AnyAsync(s => s.Id == request.SupplierId.Value && s.OrganizationId == request.OrganizationId && !s.IsDeleted, cancellationToken);
            if (!supplierExists)
            {
                throw new KeyNotFoundException($"Supplier '{request.SupplierId.Value}' not found.");
            }
        }

        var userId = _currentUser.UserId ?? "system";
        var expenseNumber = $"EXP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..18].ToUpperInvariant();

        var expense = new OperatingExpense(
            request.OrganizationId,
            expenseNumber,
            request.Category,
            request.Description,
            request.Amount,
            request.ExpenseDate,
            userId,
            request.PaymentMethod,
            request.SupplierId,
            request.Currency,
            request.Reference);

        _dbContext.OperatingExpenses.Add(expense);

        var auditLog = AuditLog.Create(
            userId,
            AuditActions.ExpenseCreated,
            AuditResourceTypes.OperatingExpense,
            expense.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Created expense '{expense.ExpenseNumber}' ({expense.Category}) for {expense.Amount} {expense.Currency}.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new ExpenseCreatedDomainEvent(
            expense.Id,
            expense.OrganizationId,
            expense.ExpenseNumber,
            expense.Category,
            expense.Amount,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return expense.Id;
    }
}

/// <summary>Handler for <see cref="ApproveOperatingExpenseCommand"/>.</summary>
public sealed class ApproveOperatingExpenseCommandHandler : IRequestHandler<ApproveOperatingExpenseCommand, Unit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxService _outbox;

    public ApproveOperatingExpenseCommandHandler(
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

    public async Task<Unit> Handle(ApproveOperatingExpenseCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var hasPermission = await _orgContext.HasPermissionAsync(request.OrganizationId, Domain.Permissions.Permissions.ExpensesManage, cancellationToken);
        if (!hasPermission)
        {
            throw new UnauthorizedAccessException("You do not have permission to approve operating expenses.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization '{request.OrganizationId}' not found.");

        if (org.Status == OrganizationStatus.Suspended)
        {
            throw new InvalidOperationException("Operation not permitted. The organization is suspended.");
        }

        var expense = await _dbContext.OperatingExpenses
            .FirstOrDefaultAsync(e => e.Id == request.ExpenseId && e.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Operating expense '{request.ExpenseId}' not found.");

        var userId = _currentUser.UserId ?? "system";
        var now = DateTime.UtcNow;

        expense.Approve(userId, now);

        var auditLog = AuditLog.Create(
            userId,
            AuditActions.ExpenseApproved,
            AuditResourceTypes.OperatingExpense,
            expense.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Approved expense '{expense.ExpenseNumber}'.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new ExpenseApprovedDomainEvent(
            expense.Id,
            expense.OrganizationId,
            expense.ExpenseNumber,
            userId,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Handler for <see cref="PayOperatingExpenseCommand"/>.</summary>
public sealed class PayOperatingExpenseCommandHandler : IRequestHandler<PayOperatingExpenseCommand, Unit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ITransactionPinService _pinService;
    private readonly ILedgerPostingService _ledgerService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IOutboxService _outbox;

    public PayOperatingExpenseCommandHandler(
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

    public async Task<Unit> Handle(PayOperatingExpenseCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var hasPermission = await _orgContext.HasPermissionAsync(request.OrganizationId, Domain.Permissions.Permissions.ExpensesManage, cancellationToken);
        if (!hasPermission)
        {
            throw new UnauthorizedAccessException("You do not have permission to pay operating expenses.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization '{request.OrganizationId}' not found.");

        if (org.Status == OrganizationStatus.Suspended)
        {
            throw new InvalidOperationException("Operation not permitted. The organization is suspended.");
        }

        var expense = await _dbContext.OperatingExpenses
            .FirstOrDefaultAsync(e => e.Id == request.ExpenseId && e.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Operating expense '{request.ExpenseId}' not found.");

        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User authentication context is required.");

        var now = DateTime.UtcNow;
        Guid? walletId = null;
        Guid? ledgerTxId = null;

        await using var tx = await _dbContext.BeginTransactionAsync(cancellationToken);
        try
        {

        if (request.PaymentMethod == ExpensePaymentMethod.Wallet)
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

            // Check Idempotency if key provided
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var existingRecord = await _idempotencyService.GetRecordAsync(
                    request.IdempotencyKey,
                    "PayOperatingExpense",
                    userId,
                    request.OrganizationId,
                    cancellationToken);

                if (existingRecord != null && existingRecord.Status == IdempotencyStatus.Completed)
                {
                    return Unit.Value;
                }
            }

            // Resolve organization wallet
            var orgWallet = await _dbContext.Wallets
                .FirstOrDefaultAsync(w => w.OrganizationId == request.OrganizationId && w.Currency == expense.Currency, cancellationToken)
                ?? throw new InvalidOperationException($"No wallet found for organization '{request.OrganizationId}' in currency '{expense.Currency}'.");

            if (orgWallet.Status != WalletStatus.Active)
            {
                throw new InvalidOperationException($"Organization wallet is {orgWallet.Status}.");
            }

            if (orgWallet.AvailableBalance < expense.Amount)
            {
                throw new InsufficientFundsException(orgWallet.AvailableBalance, expense.Amount);
            }

            // Resolve organization ledger account and platform settlement account
            var sourceLedgerAccount = await _dbContext.LedgerAccounts
                .FirstOrDefaultAsync(l => l.WalletId == orgWallet.Id, cancellationToken)
                ?? throw new InvalidOperationException("Source ledger account for organization wallet not found.");

            var systemExpenseAccount = await _ledgerService.GetOrCreateSystemSettlementAccountAsync(expense.Currency, cancellationToken);

            // Post double-entry transaction
            var ledgerTx = await _ledgerService.PostSingleCurrencyTransactionAsync(
                sourceLedgerAccount.Id,
                systemExpenseAccount.Id,
                expense.Amount,
                expense.Currency,
                LedgerTransactionType.ErpExpense,
                reference: expense.ExpenseNumber,
                idempotencyKey: request.IdempotencyKey,
                description: $"Operating expense: {expense.Description}",
                cancellationToken: cancellationToken);

            walletId = orgWallet.Id;
            ledgerTxId = ledgerTx.Id;

            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var record = await _idempotencyService.CreateRecordAsync(
                    request.IdempotencyKey,
                    "PayOperatingExpense",
                    $"{expense.Id}:{expense.Amount}",
                    userId,
                    request.OrganizationId,
                    autoSave: false,
                    cancellationToken: cancellationToken);
                await _idempotencyService.CompleteRecordAsync(record.Id, "{\"status\":\"success\"}", cancellationToken);
            }
        }

        expense.MarkPaid(now, walletId, ledgerTxId, request.Reference);

        var auditLog = AuditLog.Create(
            userId,
            AuditActions.ExpensePaid,
            AuditResourceTypes.OperatingExpense,
            expense.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Paid expense '{expense.ExpenseNumber}' for {expense.Amount} {expense.Currency} via {request.PaymentMethod}.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new ExpensePaidDomainEvent(
            expense.Id,
            expense.OrganizationId,
            expense.ExpenseNumber,
            expense.Amount,
            request.PaymentMethod,
            ledgerTxId,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Unit.Value;
    }
    catch
    {
        await tx.RollbackAsync(cancellationToken);
        throw;
    }
}
}

/// <summary>Handler for <see cref="CancelOperatingExpenseCommand"/>.</summary>
public sealed class CancelOperatingExpenseCommandHandler : IRequestHandler<CancelOperatingExpenseCommand, Unit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxService _outbox;

    public CancelOperatingExpenseCommandHandler(
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

    public async Task<Unit> Handle(CancelOperatingExpenseCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var expense = await _dbContext.OperatingExpenses
            .FirstOrDefaultAsync(e => e.Id == request.ExpenseId && e.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Operating expense '{request.ExpenseId}' not found.");

        var userId = _currentUser.UserId ?? "system";
        var now = DateTime.UtcNow;

        expense.Cancel(now);

        var auditLog = AuditLog.Create(
            userId,
            AuditActions.ExpenseCancelled,
            AuditResourceTypes.OperatingExpense,
            expense.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Cancelled expense '{expense.ExpenseNumber}'.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new ExpenseCancelledDomainEvent(
            expense.Id,
            expense.OrganizationId,
            expense.ExpenseNumber,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Handler for <see cref="GetOperatingExpensesQuery"/>.</summary>
public sealed class GetOperatingExpensesQueryHandler : IRequestHandler<GetOperatingExpensesQuery, PagedResult<OperatingExpenseDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    public GetOperatingExpensesQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    public async Task<PagedResult<OperatingExpenseDto>> Handle(GetOperatingExpensesQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var query = _dbContext.OperatingExpenses
            .Where(e => e.OrganizationId == request.OrganizationId);

        if (request.Category.HasValue)
        {
            query = query.Where(e => e.Category == request.Category.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(e => e.Status == request.Status.Value);
        }

        if (request.SupplierId.HasValue)
        {
            query = query.Where(e => e.SupplierId == request.SupplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(e => e.ExpenseNumber.ToLower().Contains(term) || e.Description.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.ExpenseDate)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new OperatingExpenseDto(
                e.Id,
                e.OrganizationId,
                e.ExpenseNumber,
                e.Category,
                e.Description,
                e.Amount,
                e.Currency,
                e.ExpenseDate,
                e.SupplierId,
                e.PaymentMethod,
                e.Status,
                e.WalletId,
                e.LedgerTransactionId,
                e.Reference,
                e.CreatedByUserId,
                e.ApprovedByUserId,
                e.ApprovedAtUtc,
                e.PaidAtUtc,
                e.CreatedAtUtc,
                e.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<OperatingExpenseDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}

/// <summary>Handler for <see cref="GetOperatingExpenseByIdQuery"/>.</summary>
public sealed class GetOperatingExpenseByIdQueryHandler : IRequestHandler<GetOperatingExpenseByIdQuery, OperatingExpenseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    public GetOperatingExpenseByIdQueryHandler(IApplicationDbContext dbContext, ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    public async Task<OperatingExpenseDto> Handle(GetOperatingExpenseByIdQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var e = await _dbContext.OperatingExpenses
            .FirstOrDefaultAsync(e => e.Id == request.ExpenseId && e.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Operating expense '{request.ExpenseId}' not found.");

        return new OperatingExpenseDto(
            e.Id,
            e.OrganizationId,
            e.ExpenseNumber,
            e.Category,
            e.Description,
            e.Amount,
            e.Currency,
            e.ExpenseDate,
            e.SupplierId,
            e.PaymentMethod,
            e.Status,
            e.WalletId,
            e.LedgerTransactionId,
            e.Reference,
            e.CreatedByUserId,
            e.ApprovedByUserId,
            e.ApprovedAtUtc,
            e.PaidAtUtc,
            e.CreatedAtUtc,
            e.UpdatedAtUtc);
    }
}

#pragma warning restore CA1862, CA1304, CA1311
