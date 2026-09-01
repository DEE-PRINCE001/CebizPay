#pragma warning disable CS1591
using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Reconciliation;

// =========================================================================
// 1. DTOs
// =========================================================================

public sealed record ReconciliationRecordDto(
    Guid Id,
    ReconciliationType ReconciliationType,
    string SourceReference,
    string Provider,
    string? ProviderReference,
    decimal? ExpectedAmount,
    decimal? ReconciledAmount,
    Currency? Currency,
    ReconciliationStatus Status,
    string? DiscrepancyReason,
    int AttemptCount,
    int MaxAttempts,
    DateTime? NextPollAtUtc,
    DateTime? LastPolledAtUtc,
    DateTime? ResolvedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record RecoveryOutstandingRecordDto(
    Guid Id,
    Guid WalletId,
    string SourceTransactionType,
    string SourceReference,
    PaymentProvider Provider,
    decimal AmountOwed,
    decimal AmountRecovered,
    Currency Currency,
    string Reason,
    RecoveryStatus Status,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc,
    string? LastActionDetails);

public sealed record WebhookEventSummaryDto(
    Guid Id,
    string Provider,
    string ProviderEventId,
    string EventType,
    string? CorrelationReference,
    WebhookEventStatus Status,
    int AttemptCount,
    int MaxAttempts,
    DateTime ReceivedAtUtc,
    DateTime? ProcessedAtUtc,
    DateTime? NextRetryAtUtc,
    string? ProcessingError);

// =========================================================================
// 2. QUERIES
// =========================================================================

public sealed record GetReconciliationRecordsQuery(
    ReconciliationType? Type = null,
    ReconciliationStatus? Status = null,
    string? Provider = null,
    int PageNumber = 1,
    int PageSize = 50) : IRequest<IReadOnlyList<ReconciliationRecordDto>>;

public sealed class GetReconciliationRecordsQueryHandler : IRequestHandler<GetReconciliationRecordsQuery, IReadOnlyList<ReconciliationRecordDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetReconciliationRecordsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<ReconciliationRecordDto>> Handle(GetReconciliationRecordsQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.ReconciliationRecords.AsQueryable();

        if (request.Type.HasValue)
            query = query.Where(r => r.ReconciliationType == request.Type.Value);

        if (request.Status.HasValue)
            query = query.Where(r => r.Status == request.Status.Value);

        if (!string.IsNullOrWhiteSpace(request.Provider))
            query = query.Where(r => r.Provider == request.Provider.Trim());

        var page = request.PageNumber > 0 ? request.PageNumber : 1;
        var size = request.PageSize > 0 && request.PageSize <= 100 ? request.PageSize : 50;

        var records = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(r => new ReconciliationRecordDto(
                r.Id,
                r.ReconciliationType,
                r.SourceReference,
                r.Provider,
                r.ProviderReference,
                r.ExpectedAmount,
                r.ReconciledAmount,
                r.Currency,
                r.Status,
                r.DiscrepancyReason,
                r.AttemptCount,
                r.MaxAttempts,
                r.NextPollAtUtc,
                r.LastPolledAtUtc,
                r.ResolvedAtUtc,
                r.CreatedAtUtc,
                r.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return records;
    }
}

public sealed record GetOutstandingRecoveriesQuery(
    Guid? WalletId = null,
    RecoveryStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 50) : IRequest<IReadOnlyList<RecoveryOutstandingRecordDto>>;

public sealed class GetOutstandingRecoveriesQueryHandler : IRequestHandler<GetOutstandingRecoveriesQuery, IReadOnlyList<RecoveryOutstandingRecordDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetOutstandingRecoveriesQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<RecoveryOutstandingRecordDto>> Handle(GetOutstandingRecoveriesQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.RecoveryOutstandingRecords.AsQueryable();

        if (request.WalletId.HasValue)
            query = query.Where(r => r.WalletId == request.WalletId.Value);

        if (request.Status.HasValue)
            query = query.Where(r => r.Status == request.Status.Value);

        var page = request.PageNumber > 0 ? request.PageNumber : 1;
        var size = request.PageSize > 0 && request.PageSize <= 100 ? request.PageSize : 50;

        var items = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(r => new RecoveryOutstandingRecordDto(
                r.Id,
                r.WalletId,
                r.SourceTransactionType,
                r.SourceReference,
                r.Provider,
                r.AmountOwed,
                r.AmountRecovered,
                r.Currency,
                r.Reason,
                r.Status,
                r.CreatedAtUtc,
                r.ResolvedAtUtc,
                r.LastActionDetails))
            .ToListAsync(cancellationToken);

        return items;
    }
}

// =========================================================================
// 3. COMMANDS
// =========================================================================

public sealed record RetryWebhookEventCommand(
    Guid EventId,
    bool IsComplianceEvent = false) : IRequest<bool>;

public sealed class RetryWebhookEventCommandHandler : IRequestHandler<RetryWebhookEventCommand, bool>
{
    private readonly IWebhookProcessingService _processingService;

    public RetryWebhookEventCommandHandler(IWebhookProcessingService processingService)
    {
        _processingService = processingService ?? throw new ArgumentNullException(nameof(processingService));
    }

    public async Task<bool> Handle(RetryWebhookEventCommand request, CancellationToken cancellationToken)
    {
        if (request.IsComplianceEvent)
        {
            var res = await _processingService.ProcessSingleComplianceWebhookAsync(request.EventId, cancellationToken);
            return res.Status == CebizPay.Application.Common.Interfaces.Compliance.ComplianceWebhookProcessingStatus.Processed;
        }
        else
        {
            var res = await _processingService.ProcessSingleFinancialWebhookAsync(request.EventId, cancellationToken);
            return res.Status == WebhookProcessingStatus.Processed;
        }
    }
}

public sealed record RequeryPaymentStatusCommand(string Reference) : IRequest<UnifiedReconciliationResult>;

public sealed class RequeryPaymentStatusCommandHandler : IRequestHandler<RequeryPaymentStatusCommand, UnifiedReconciliationResult>
{
    private readonly IReconciliationEngine _reconciliationEngine;

    public RequeryPaymentStatusCommandHandler(IReconciliationEngine reconciliationEngine)
    {
        _reconciliationEngine = reconciliationEngine ?? throw new ArgumentNullException(nameof(reconciliationEngine));
    }

    public async Task<UnifiedReconciliationResult> Handle(RequeryPaymentStatusCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reference))
            throw new ArgumentException("Reference is required.", nameof(request));

        return await _reconciliationEngine.RequeryByReferenceAsync(request.Reference, cancellationToken);
    }
}

public sealed record SubmitManualReviewDecisionCommand(
    Guid ReconciliationRecordId,
    ManualReviewDecision Decision,
    string ReviewerNotes,
    string ReviewerUserId) : IRequest<UnifiedReconciliationResult>;

public sealed class SubmitManualReviewDecisionCommandHandler : IRequestHandler<SubmitManualReviewDecisionCommand, UnifiedReconciliationResult>
{
    private readonly IReconciliationEngine _reconciliationEngine;

    public SubmitManualReviewDecisionCommandHandler(IReconciliationEngine reconciliationEngine)
    {
        _reconciliationEngine = reconciliationEngine ?? throw new ArgumentNullException(nameof(reconciliationEngine));
    }

    public async Task<UnifiedReconciliationResult> Handle(SubmitManualReviewDecisionCommand request, CancellationToken cancellationToken)
    {
        if (request.ReconciliationRecordId == Guid.Empty)
            throw new ArgumentException("ReconciliationRecordId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ReviewerNotes))
            throw new ArgumentException("ReviewerNotes are required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ReviewerUserId))
            throw new ArgumentException("ReviewerUserId is required.", nameof(request));

        return await _reconciliationEngine.ResolveManualReviewAsync(
            request.ReconciliationRecordId,
            request.Decision,
            request.ReviewerNotes,
            request.ReviewerUserId,
            cancellationToken);
    }
}
