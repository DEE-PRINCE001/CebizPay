using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payroll;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payroll.Entities;
using CebizPay.Domain.Payroll.Enums;
using CebizPay.Domain.Payroll.Events;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Payroll;

/// <summary>
/// Domain service coordinating payroll batch creation, querying, progress aggregation, retries, cancellation, and payment voucher maintenance.
/// </summary>
public sealed partial class PayrollBatchService : IPayrollBatchService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPayrollCalculationService _calculationService;
    private readonly IOutboxService _outbox;
    private readonly ILogger<PayrollBatchService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="PayrollBatchService"/>.
    /// </summary>
    public PayrollBatchService(
        ApplicationDbContext dbContext,
        IPayrollCalculationService calculationService,
        IOutboxService outbox,
        ILogger<PayrollBatchService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _calculationService = calculationService ?? throw new ArgumentNullException(nameof(calculationService));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<PayrollBatchDto> CreateAndEnqueueBatchAsync(
        Guid organizationId,
        string initiatorUserId,
        Currency currency,
        DateTime periodStart,
        DateTime periodEnd,
        PayrollSelectionCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(initiatorUserId))
            throw new ArgumentException("InitiatorUserId is required.", nameof(initiatorUserId));

        currency.EnsureTransactionalV1();
        criteria ??= new PayrollSelectionCriteria();

        // 1. Verify organization eligibility
        var org = await _dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (org == null || org.IsDeleted)
        {
            throw new InvalidOperationException($"Organization '{organizationId}' not found.");
        }

        if (org.Status == OrganizationStatus.Suspended)
        {
            throw new InvalidOperationException("Suspended organizations are not permitted to execute payroll.");
        }

        if (!org.CanExecutePayroll())
        {
            throw new InvalidOperationException("Organization must be fully verified and approved before executing payroll.");
        }

        // Verify initiator holds active membership with Payroll.Execute permission
        var membership = await _dbContext.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == initiatorUserId && m.Status == MembershipStatus.Active, cancellationToken)
            .ConfigureAwait(false);

        if (membership == null || !membership.HasPermission(Domain.Permissions.Permissions.PayrollExecute))
        {
            throw new UnauthorizedAccessException("Initiator does not have permission to execute payroll for this organization.");
        }

        // 2. Perform deterministic calculation dry-run to snapshot items
        var calcResult = await _calculationService.CalculatePayrollAsync(organizationId, currency, criteria, cancellationToken).ConfigureAwait(false);
        if (calcResult.Items.Count == 0)
        {
            throw new InvalidOperationException("No eligible active employees found matching the specified selection criteria.");
        }

        // 3. Pre-check Organization wallet balance sufficiency
        var orgWallet = await _dbContext.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.OrganizationId == organizationId && w.Currency == currency, cancellationToken)
            .ConfigureAwait(false);

        if (orgWallet == null)
        {
            throw new InvalidOperationException($"Organization wallet for currency '{currency}' not found.");
        }

        if (orgWallet.AvailableBalance < calcResult.TotalNetAmount)
        {
            throw new InvalidOperationException(
                $"Insufficient organization wallet balance. Required: {calcResult.TotalNetAmount:F2} {currency}, Available: {orgWallet.AvailableBalance:F2} {currency}.");
        }

        // 4. Create Batch aggregate
        var criteriaJson = JsonSerializer.Serialize(criteria);
        var batch = PayrollBatch.Create(
            organizationId: organizationId,
            currency: currency,
            selectionMode: criteria.Mode,
            periodStart: periodStart,
            periodEnd: periodEnd,
            createdByUserId: initiatorUserId,
            selectionCriteriaJson: criteriaJson);

        // 5. Populate and snapshot item lines
        foreach (var itemDto in calcResult.Items)
        {
            var deductionsJson = itemDto.Deductions != null && itemDto.Deductions.Count > 0
                ? JsonSerializer.Serialize(itemDto.Deductions)
                : null;

            var item = PayrollItem.Create(
                payrollBatchId: batch.Id,
                organizationId: organizationId,
                employeeUserId: itemDto.EmployeeUserId,
                employeeName: itemDto.EmployeeName,
                employeeEmail: itemDto.EmployeeEmail,
                currency: currency,
                grossPay: itemDto.GrossPay,
                totalDeductions: itemDto.TotalDeductions,
                departmentId: itemDto.DepartmentId,
                workforceRoleId: itemDto.WorkforceRoleId,
                salaryLevelId: itemDto.SalaryLevelId,
                deductionsDetailJson: deductionsJson);

            batch.AddItem(item);
        }

        _dbContext.PayrollBatches.Add(batch);

        // 6. Record Audit and enqueue Outbox event
        var audit = AuditLog.Create(
            actorId: initiatorUserId,
            action: AuditActions.PayrollCreated,
            resourceType: AuditResourceTypes.PayrollBatch,
            resourceId: batch.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new
            {
                batch.BatchReference,
                batch.TotalEmployees,
                batch.TotalGrossAmount,
                batch.TotalDeductionsAmount,
                batch.TotalNetAmount,
                Currency = currency.ToString(),
                batch.PeriodStart,
                batch.PeriodEnd
            }),
            organizationId: organizationId);
        _dbContext.AuditLogs.Add(audit);

        _outbox.Write(new PayrollBatchCreatedDomainEvent(
            PayrollBatchId: batch.Id,
            BatchReference: batch.BatchReference,
            OrganizationId: organizationId,
            Currency: currency,
            TotalEmployees: batch.TotalEmployees,
            TotalNetAmount: batch.TotalNetAmount,
            OccurredOnUtc: DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogPayrollBatchCreated(_logger, batch.BatchReference, batch.TotalEmployees, batch.TotalNetAmount, currency, organizationId);

        return new PayrollBatchDto(
            BatchId: batch.Id,
            BatchReference: batch.BatchReference,
            OrganizationId: batch.OrganizationId,
            Currency: batch.Currency,
            Status: batch.Status,
            TotalEmployees: batch.TotalEmployees,
            TotalGrossAmount: batch.TotalGrossAmount,
            TotalDeductionsAmount: batch.TotalDeductionsAmount,
            TotalNetAmount: batch.TotalNetAmount,
            PeriodStart: batch.PeriodStart,
            PeriodEnd: batch.PeriodEnd,
            CreatedAtUtc: batch.CreatedAtUtc);
    }

    /// <inheritdoc/>
    public async Task<PayrollBatchProgressDto?> GetBatchProgressAsync(
        Guid organizationId,
        Guid batchId,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var batch = await _dbContext.PayrollBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == batchId && b.OrganizationId == organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (batch == null)
            return null;

        var itemsQuery = _dbContext.PayrollItems
            .AsNoTracking()
            .Where(i => i.PayrollBatchId == batchId);

        // Compute aggregate counts via efficient SQL expressions
        var totalCount = await itemsQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var completedCount = await itemsQuery.CountAsync(i => i.Status == PayrollItemStatus.Completed, cancellationToken).ConfigureAwait(false);
        var processingCount = await itemsQuery.CountAsync(i => i.Status == PayrollItemStatus.Processing, cancellationToken).ConfigureAwait(false);
        var pendingCount = await itemsQuery.CountAsync(i => i.Status == PayrollItemStatus.Pending, cancellationToken).ConfigureAwait(false);
        var failedCount = await itemsQuery.CountAsync(i => i.Status == PayrollItemStatus.Failed, cancellationToken).ConfigureAwait(false);
        var retryPendingCount = await itemsQuery.CountAsync(i => i.Status == PayrollItemStatus.RetryPending, cancellationToken).ConfigureAwait(false);

        var progressPercentage = totalCount > 0
            ? Math.Round((decimal)completedCount / totalCount * 100m, 2)
            : 0m;

        // Paged item details
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var pagedItems = await itemsQuery
            .OrderBy(i => i.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new PayrollItemProgressDto(
                i.Id,
                i.EmployeeUserId,
                i.EmployeeName,
                i.EmployeeEmail,
                i.GrossPay,
                i.TotalDeductions,
                i.NetPay,
                i.Currency,
                i.Status,
                i.CurrentAttemptNumber,
                i.LastFailureCode,
                i.LastFailureReason,
                i.PaymentVoucherId,
                i.LedgerTransactionId,
                i.CreatedAtUtc,
                i.UpdatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PayrollBatchProgressDto(
            BatchId: batch.Id,
            BatchReference: batch.BatchReference,
            OrganizationId: batch.OrganizationId,
            Currency: batch.Currency,
            Status: batch.Status,
            TotalEmployees: totalCount,
            CompletedCount: completedCount,
            ProcessingCount: processingCount,
            PendingCount: pendingCount,
            FailedCount: failedCount,
            RetryPendingCount: retryPendingCount,
            ProgressPercentage: progressPercentage,
            TotalGrossAmount: batch.TotalGrossAmount,
            TotalDeductionsAmount: batch.TotalDeductionsAmount,
            TotalNetAmount: batch.TotalNetAmount,
            CreatedAtUtc: batch.CreatedAtUtc,
            StartedAtUtc: batch.StartedAtUtc,
            CompletedAtUtc: batch.CompletedAtUtc,
            FailureReason: batch.FailureReason,
            Items: pagedItems);
    }

    /// <inheritdoc/>
    public async Task<int> RetryFailedItemsAsync(
        Guid organizationId,
        Guid batchId,
        string initiatorUserId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _dbContext.PayrollBatches
            .FirstOrDefaultAsync(b => b.Id == batchId && b.OrganizationId == organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (batch == null)
            throw new InvalidOperationException($"PayrollBatch '{batchId}' not found.");

        var failedItems = await _dbContext.PayrollItems
            .Where(i => i.PayrollBatchId == batchId && i.Status == PayrollItemStatus.Failed)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (failedItems.Count == 0)
            return 0;

        foreach (var item in failedItems)
        {
            item.QueueForRetry();

            _outbox.Write(new PayrollItemRetriedDomainEvent(
                PayrollBatchId: batch.Id,
                PayrollItemId: item.Id,
                OrganizationId: organizationId,
                OccurredOnUtc: DateTime.UtcNow));
        }

        // Reopen batch processing state if it was previously closed as PartiallyCompleted or Failed
        if (batch.Status == PayrollBatchStatus.PartiallyCompleted || batch.Status == PayrollBatchStatus.Failed)
        {
            batch.MarkProcessing();
        }

        var audit = AuditLog.Create(
            actorId: initiatorUserId,
            action: AuditActions.PayrollItemRetried,
            resourceType: AuditResourceTypes.PayrollBatch,
            resourceId: batch.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new { batch.BatchReference, RetriedItemsCount = failedItems.Count }),
            organizationId: organizationId);
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogPayrollItemsRetried(_logger, failedItems.Count, batch.BatchReference, organizationId);
        return failedItems.Count;
    }

    /// <inheritdoc/>
    public async Task CancelBatchAsync(
        Guid organizationId,
        Guid batchId,
        string initiatorUserId,
        CancellationToken cancellationToken = default)
    {
        var batch = await _dbContext.PayrollBatches
            .FirstOrDefaultAsync(b => b.Id == batchId && b.OrganizationId == organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (batch == null)
            throw new InvalidOperationException($"PayrollBatch '{batchId}' not found.");

        batch.Cancel();

        var audit = AuditLog.Create(
            actorId: initiatorUserId,
            action: AuditActions.PayrollCancelled,
            resourceType: AuditResourceTypes.PayrollBatch,
            resourceId: batch.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new { batch.BatchReference }),
            organizationId: organizationId);
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogPayrollBatchCancelled(_logger, batch.BatchReference, organizationId);
    }

    /// <inheritdoc/>
    public async Task<PaymentVoucherDto?> GetPaymentVoucherByIdAsync(
        Guid organizationId,
        Guid voucherId,
        CancellationToken cancellationToken = default)
    {
        var voucher = await _dbContext.PaymentVouchers
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == voucherId && v.OrganizationId == organizationId, cancellationToken)
            .ConfigureAwait(false);

        return voucher == null ? null : MapToVoucherDto(voucher);
    }

    /// <inheritdoc/>
    public async Task<PaymentVoucherDto> UpdatePaymentVoucherMetadataAsync(
        Guid organizationId,
        Guid voucherId,
        string initiatorUserId,
        UpdatePaymentVoucherMetadataRequest request,
        CancellationToken cancellationToken = default)
    {
        var voucher = await _dbContext.PaymentVouchers
            .FirstOrDefaultAsync(v => v.Id == voucherId && v.OrganizationId == organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (voucher == null)
            throw new InvalidOperationException($"PaymentVoucher '{voucherId}' not found.");

        var beforeState = JsonSerializer.Serialize(new { voucher.BankName, voucher.Remarks, voucher.Description });

        voucher.UpdateMetadata(request.BankName, request.Remarks, request.Description);

        var afterState = JsonSerializer.Serialize(new { voucher.BankName, voucher.Remarks, voucher.Description });

        var audit = AuditLog.Create(
            actorId: initiatorUserId,
            action: AuditActions.PaymentVoucherMetadataUpdated,
            resourceType: AuditResourceTypes.PaymentVoucher,
            resourceId: voucher.Id.ToString(),
            beforeJson: beforeState,
            afterJson: afterState,
            organizationId: organizationId);
        _dbContext.AuditLogs.Add(audit);

        _outbox.Write(new PaymentVoucherMetadataUpdatedDomainEvent(
            PaymentVoucherId: voucher.Id,
            VoucherReference: voucher.VoucherReference,
            OrganizationId: organizationId,
            BankName: voucher.BankName,
            Remarks: voucher.Remarks,
            OccurredOnUtc: DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogVoucherMetadataUpdated(_logger, voucher.VoucherReference, organizationId);
        return MapToVoucherDto(voucher);
    }

    /// <inheritdoc/>
    public async Task<PayrollAnalyticsDto> GetOrganizationPayrollAnalyticsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var totalBatches = await _dbContext.PayrollBatches
            .AsNoTracking()
            .CountAsync(b => b.OrganizationId == organizationId, cancellationToken)
            .ConfigureAwait(false);

        var completedItems = _dbContext.PayrollItems
            .AsNoTracking()
            .Where(i => i.OrganizationId == organizationId && i.Status == PayrollItemStatus.Completed);

        var totalDisbursedCount = await completedItems.CountAsync(cancellationToken).ConfigureAwait(false);

        var totalNgn = await completedItems
            .Where(i => i.Currency == Currency.NGN)
            .SumAsync(i => (decimal?)i.NetPay, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        var totalIntNgn = await completedItems
            .Where(i => i.Currency == Currency.INTERNATIONAL_NGN)
            .SumAsync(i => (decimal?)i.NetPay, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        var totalUsdt = await completedItems
            .Where(i => i.Currency == Currency.USDT)
            .SumAsync(i => (decimal?)i.NetPay, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        var lastExecution = await _dbContext.PayrollBatches
            .AsNoTracking()
            .Where(b => b.OrganizationId == organizationId && (b.Status == PayrollBatchStatus.Completed || b.Status == PayrollBatchStatus.PartiallyCompleted))
            .OrderByDescending(b => b.CompletedAtUtc)
            .Select(b => b.CompletedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PayrollAnalyticsDto(
            OrganizationId: organizationId,
            TotalBatchesCount: totalBatches,
            TotalDisbursedItemsCount: totalDisbursedCount,
            TotalDisbursedNgn: totalNgn,
            TotalDisbursedInternationalNgn: totalIntNgn,
            TotalDisbursedUsdt: totalUsdt,
            LastPayrollExecutedAtUtc: lastExecution);
    }

    private static PaymentVoucherDto MapToVoucherDto(PaymentVoucher voucher) => new(
        Id: voucher.Id,
        VoucherReference: voucher.VoucherReference,
        PayrollBatchId: voucher.PayrollBatchId,
        PayrollItemId: voucher.PayrollItemId,
        LedgerTransactionId: voucher.LedgerTransactionId,
        OrganizationId: voucher.OrganizationId,
        EmployeeUserId: voucher.EmployeeUserId,
        EmployeeName: voucher.EmployeeName,
        GrossPay: voucher.GrossPay,
        Deductions: voucher.Deductions,
        NetPay: voucher.NetPay,
        Currency: voucher.Currency,
        Status: voucher.Status,
        BankName: voucher.BankName,
        Remarks: voucher.Remarks,
        Description: voucher.Description,
        CreatedAtUtc: voucher.CreatedAtUtc,
        UpdatedAtUtc: voucher.UpdatedAtUtc);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Enqueued PayrollBatch {BatchReference} with {TotalEmployees} items ({TotalNetAmount} {Currency}) for Organization {OrganizationId}")]
    private static partial void LogPayrollBatchCreated(ILogger logger, string batchReference, int totalEmployees, decimal totalNetAmount, Currency currency, Guid organizationId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Queued {Count} failed items for retry in PayrollBatch {BatchReference} (Org {OrganizationId})")]
    private static partial void LogPayrollItemsRetried(ILogger logger, int count, string batchReference, Guid organizationId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Cancelled PayrollBatch {BatchReference} for Organization {OrganizationId}")]
    private static partial void LogPayrollBatchCancelled(ILogger logger, string batchReference, Guid organizationId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Updated metadata for PaymentVoucher {VoucherReference} (Org {OrganizationId})")]
    private static partial void LogVoucherMetadataUpdated(ILogger logger, string voucherReference, Guid organizationId);
}
