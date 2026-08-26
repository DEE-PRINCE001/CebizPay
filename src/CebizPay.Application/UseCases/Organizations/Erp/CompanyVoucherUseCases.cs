#pragma warning disable CS1591, CA1862, CA1304, CA1311
using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models;
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

// ==========================================
// Commands & Queries
// ==========================================

/// <summary>Command to create a draft company voucher.</summary>
public sealed record CreateCompanyVoucherCommand(
    Guid OrganizationId,
    string PayeeName,
    string Purpose,
    decimal Amount,
    Currency Currency = Currency.NGN,
    CompanyVoucherPaymentMethod PaymentMethod = CompanyVoucherPaymentMethod.Manual,
    string? PayeeDetails = null,
    string? Notes = null,
    string? Reference = null) : IRequest<Guid>;

/// <summary>Command to approve a draft company voucher.</summary>
public sealed record ApproveCompanyVoucherCommand(
    Guid OrganizationId,
    Guid VoucherId) : IRequest<Unit>;

/// <summary>Command to pay/settle an approved company voucher.</summary>
public sealed record PayCompanyVoucherCommand(
    Guid OrganizationId,
    Guid VoucherId,
    CompanyVoucherPaymentMethod PaymentMethod,
    string? Pin = null,
    string? IdempotencyKey = null,
    string? Reference = null) : IRequest<Unit>;

/// <summary>Command to cancel a company voucher.</summary>
public sealed record CancelCompanyVoucherCommand(
    Guid OrganizationId,
    Guid VoucherId) : IRequest<Unit>;

/// <summary>Query to retrieve paged company vouchers.</summary>
public sealed record GetCompanyVouchersQuery(
    Guid OrganizationId,
    CompanyVoucherStatus? Status = null,
    string? Search = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<CompanyVoucherDto>>;

/// <summary>Query to retrieve a single company voucher by ID.</summary>
public sealed record GetCompanyVoucherByIdQuery(
    Guid OrganizationId,
    Guid VoucherId) : IRequest<CompanyVoucherDto>;

// ==========================================
// Command Handlers
// ==========================================

/// <summary>Handler for <see cref="CreateCompanyVoucherCommand"/>.</summary>
public sealed class CreateCompanyVoucherCommandHandler : IRequestHandler<CreateCompanyVoucherCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxService _outbox;

    public CreateCompanyVoucherCommandHandler(
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

    public async Task<Guid> Handle(CreateCompanyVoucherCommand request, CancellationToken cancellationToken)
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

        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User authentication context is required.");

        var voucherNumber = $"CV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        var voucher = new CompanyVoucher(
            request.OrganizationId,
            voucherNumber,
            request.PayeeName,
            request.Purpose,
            request.Amount,
            userId,
            request.Currency,
            request.PaymentMethod,
            request.PayeeDetails,
            request.Notes,
            request.Reference);

        _dbContext.CompanyVouchers.Add(voucher);

        var auditLog = AuditLog.Create(
            userId,
            AuditActions.CompanyVoucherCreated,
            AuditResourceTypes.CompanyVoucher,
            voucher.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Created company voucher '{voucher.VoucherNumber}' for {voucher.Amount} {voucher.Currency}.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new CompanyVoucherCreatedDomainEvent(
            voucher.Id,
            voucher.OrganizationId,
            voucher.VoucherNumber,
            voucher.Amount,
            voucher.Currency,
            voucher.PayeeName,
            voucher.CreatedAtUtc));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return voucher.Id;
    }
}

/// <summary>Handler for <see cref="ApproveCompanyVoucherCommand"/>.</summary>
public sealed class ApproveCompanyVoucherCommandHandler : IRequestHandler<ApproveCompanyVoucherCommand, Unit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxService _outbox;

    public ApproveCompanyVoucherCommandHandler(
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

    public async Task<Unit> Handle(ApproveCompanyVoucherCommand request, CancellationToken cancellationToken)
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

        var voucher = await _dbContext.CompanyVouchers
            .FirstOrDefaultAsync(v => v.Id == request.VoucherId && v.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Company voucher '{request.VoucherId}' not found.");

        var userId = _currentUser.UserId ?? "system";
        var now = DateTime.UtcNow;

        voucher.Approve(userId, now);

        var auditLog = AuditLog.Create(
            userId,
            AuditActions.CompanyVoucherApproved,
            AuditResourceTypes.CompanyVoucher,
            voucher.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Approved company voucher '{voucher.VoucherNumber}'.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new CompanyVoucherApprovedDomainEvent(
            voucher.Id,
            voucher.OrganizationId,
            voucher.VoucherNumber,
            userId,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Handler for <see cref="PayCompanyVoucherCommand"/>.</summary>
public sealed class PayCompanyVoucherCommandHandler : IRequestHandler<PayCompanyVoucherCommand, Unit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ITransactionPinService _pinService;
    private readonly ILedgerPostingService _ledgerService;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IOutboxService _outbox;

    public PayCompanyVoucherCommandHandler(
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

    public async Task<Unit> Handle(PayCompanyVoucherCommand request, CancellationToken cancellationToken)
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

        var voucher = await _dbContext.CompanyVouchers
            .FirstOrDefaultAsync(v => v.Id == request.VoucherId && v.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Company voucher '{request.VoucherId}' not found.");

        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User authentication context is required.");

        var now = DateTime.UtcNow;
        Guid? walletId = null;
        Guid? ledgerTxId = null;

        if (request.PaymentMethod == CompanyVoucherPaymentMethod.Wallet)
        {
            if (string.IsNullOrWhiteSpace(request.Pin))
            {
                throw new ArgumentException("Transaction PIN is required for wallet disbursements.", nameof(request));
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
                    "PayCompanyVoucher",
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
                .FirstOrDefaultAsync(w => w.OrganizationId == request.OrganizationId && w.Currency == voucher.Currency, cancellationToken)
                ?? throw new InvalidOperationException($"No wallet found for organization '{request.OrganizationId}' in currency '{voucher.Currency}'.");

            if (orgWallet.Status != WalletStatus.Active)
            {
                throw new InvalidOperationException($"Organization wallet is {orgWallet.Status}.");
            }

            if (orgWallet.AvailableBalance < voucher.Amount)
            {
                throw new InsufficientFundsException(orgWallet.AvailableBalance, voucher.Amount);
            }

            // Resolve organization ledger account and platform settlement account
            var sourceLedgerAccount = await _dbContext.LedgerAccounts
                .FirstOrDefaultAsync(l => l.WalletId == orgWallet.Id, cancellationToken)
                ?? throw new InvalidOperationException("Source ledger account for organization wallet not found.");

            var systemDisbursementAccount = await _ledgerService.GetOrCreateSystemSettlementAccountAsync(voucher.Currency, cancellationToken);

            // Post double-entry transaction
            var reference = !string.IsNullOrWhiteSpace(request.Reference) ? request.Reference : voucher.VoucherNumber;
            var ledgerTx = await _ledgerService.PostSingleCurrencyTransactionAsync(
                sourceLedgerAccount.Id,
                systemDisbursementAccount.Id,
                voucher.Amount,
                voucher.Currency,
                LedgerTransactionType.CompanyVoucherDisbursement,
                reference,
                request.IdempotencyKey,
                voucher.Purpose,
                cancellationToken);

            walletId = orgWallet.Id;
            ledgerTxId = ledgerTx.Id;

            voucher.MarkPaid(now, walletId, ledgerTxId, ledgerTx.Reference);

            // Complete Idempotency record if key provided
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var record = await _idempotencyService.CreateRecordAsync(
                    request.IdempotencyKey,
                    "PayCompanyVoucher",
                    $"{voucher.Id}:{voucher.Amount}",
                    userId,
                    request.OrganizationId,
                    autoSave: false,
                    cancellationToken: cancellationToken);
                await _idempotencyService.CompleteRecordAsync(record.Id, "{\"status\":\"success\"}", cancellationToken);
            }
        }
        else
        {
            // Manual external payment
            var reference = !string.IsNullOrWhiteSpace(request.Reference) ? request.Reference : voucher.Reference;
            voucher.MarkPaid(now, reference: reference);
        }

        var auditLog = AuditLog.Create(
            userId,
            AuditActions.CompanyVoucherPaid,
            AuditResourceTypes.CompanyVoucher,
            voucher.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Settled company voucher '{voucher.VoucherNumber}' for {voucher.Amount} {voucher.Currency} via {request.PaymentMethod}.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new CompanyVoucherPaidDomainEvent(
            voucher.Id,
            voucher.OrganizationId,
            voucher.VoucherNumber,
            voucher.Amount,
            voucher.Currency,
            request.PaymentMethod,
            ledgerTxId,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Handler for <see cref="CancelCompanyVoucherCommand"/>.</summary>
public sealed class CancelCompanyVoucherCommandHandler : IRequestHandler<CancelCompanyVoucherCommand, Unit>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IOutboxService _outbox;

    public CancelCompanyVoucherCommandHandler(
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

    public async Task<Unit> Handle(CancelCompanyVoucherCommand request, CancellationToken cancellationToken)
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

        var voucher = await _dbContext.CompanyVouchers
            .FirstOrDefaultAsync(v => v.Id == request.VoucherId && v.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Company voucher '{request.VoucherId}' not found.");

        var userId = _currentUser.UserId ?? "system";
        var now = DateTime.UtcNow;

        voucher.Cancel(now);

        var auditLog = AuditLog.Create(
            userId,
            AuditActions.CompanyVoucherCancelled,
            AuditResourceTypes.CompanyVoucher,
            voucher.Id.ToString(),
            request.OrganizationId,
            afterJson: $"Cancelled company voucher '{voucher.VoucherNumber}'.");
        _dbContext.AuditLogs.Add(auditLog);

        _outbox.Write(new CompanyVoucherCancelledDomainEvent(
            voucher.Id,
            voucher.OrganizationId,
            voucher.VoucherNumber,
            now));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

// ==========================================
// Query Handlers
// ==========================================

/// <summary>Handler for <see cref="GetCompanyVouchersQuery"/>.</summary>
public sealed class GetCompanyVouchersQueryHandler : IRequestHandler<GetCompanyVouchersQuery, PagedResult<CompanyVoucherDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    public GetCompanyVouchersQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    public async Task<PagedResult<CompanyVoucherDto>> Handle(GetCompanyVouchersQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var query = _dbContext.CompanyVouchers
            .Where(v => v.OrganizationId == request.OrganizationId);

        if (request.Status.HasValue)
        {
            query = query.Where(v => v.Status == request.Status.Value);
        }

        if (request.FromUtc.HasValue)
        {
            query = query.Where(v => v.CreatedAtUtc >= request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            query = query.Where(v => v.CreatedAtUtc <= request.ToUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(v =>
                v.VoucherNumber.ToLower().Contains(search) ||
                v.PayeeName.ToLower().Contains(search) ||
                v.Purpose.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(v => v.CreatedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(v => new CompanyVoucherDto(
                v.Id,
                v.OrganizationId,
                v.VoucherNumber,
                v.PayeeName,
                v.PayeeDetails,
                v.Purpose,
                v.Amount,
                v.Currency,
                v.PaymentMethod,
                v.Status,
                v.CreatedByUserId,
                v.ApprovedByUserId,
                v.ApprovedAtUtc,
                v.PaidAtUtc,
                v.WalletId,
                v.LedgerTransactionId,
                v.Reference,
                v.Notes,
                v.CreatedAtUtc,
                v.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<CompanyVoucherDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}

/// <summary>Handler for <see cref="GetCompanyVoucherByIdQuery"/>.</summary>
public sealed class GetCompanyVoucherByIdQueryHandler : IRequestHandler<GetCompanyVoucherByIdQuery, CompanyVoucherDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    public GetCompanyVoucherByIdQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    public async Task<CompanyVoucherDto> Handle(GetCompanyVoucherByIdQuery request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("You do not have access to this organization.");
        }

        var v = await _dbContext.CompanyVouchers
            .FirstOrDefaultAsync(v => v.Id == request.VoucherId && v.OrganizationId == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Company voucher '{request.VoucherId}' not found.");

        return new CompanyVoucherDto(
            v.Id,
            v.OrganizationId,
            v.VoucherNumber,
            v.PayeeName,
            v.PayeeDetails,
            v.Purpose,
            v.Amount,
            v.Currency,
            v.PaymentMethod,
            v.Status,
            v.CreatedByUserId,
            v.ApprovedByUserId,
            v.ApprovedAtUtc,
            v.PaidAtUtc,
            v.WalletId,
            v.LedgerTransactionId,
            v.Reference,
            v.Notes,
            v.CreatedAtUtc,
            v.UpdatedAtUtc);
    }
}
