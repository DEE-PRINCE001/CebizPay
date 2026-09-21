using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payroll;
using CebizPay.Application.Common.Models;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Erp.Enums;
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
        // 1. Verify initiator holds active membership with Payroll.Execute permission
        var membership = await _dbContext.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == initiatorUserId && m.Status == MembershipStatus.Active, cancellationToken)
            .ConfigureAwait(false);

        if (membership == null || !membership.HasPermission(Domain.Permissions.Permissions.PayrollExecute))
        {
            throw new UnauthorizedAccessException("Initiator does not have permission to retry payroll items for this organization.");
        }

        var batch = await _dbContext.PayrollBatches
            .FirstOrDefaultAsync(b => b.Id == batchId && b.OrganizationId == organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (batch == null)
            throw new InvalidOperationException($"PayrollBatch '{batchId}' not found.");

        if (batch.Status == PayrollBatchStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot retry failed items for batch in status '{batch.Status}'.");
        }

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
        // 1. Verify initiator holds active membership with Payroll.Execute permission
        var membership = await _dbContext.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == initiatorUserId && m.Status == MembershipStatus.Active, cancellationToken)
            .ConfigureAwait(false);

        if (membership == null || !membership.HasPermission(Domain.Permissions.Permissions.PayrollExecute))
        {
            throw new UnauthorizedAccessException("Initiator does not have permission to cancel payroll for this organization.");
        }

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

    /// <inheritdoc/>
    public async Task<OrgPayrollAnalyticsSummaryDto> GetPortalPayrollAnalyticsSummaryAsync(
        Guid organizationId,
        int? year = null,
        string? currency = null,
        CancellationToken cancellationToken = default)
    {
        var targetYear = year.HasValue && year.Value >= 2000 ? year.Value : DateTime.UtcNow.Year;
        var priorYear = targetYear - 1;
        var baseCurrency = string.IsNullOrWhiteSpace(currency) ? "NGN" : currency.Trim().ToUpperInvariant();

        var targetYearStart = new DateTime(targetYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var targetYearEnd = targetYearStart.AddYears(1);
        var priorYearStart = new DateTime(priorYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var priorYearEnd = targetYearStart;

        var targetItems = await _dbContext.PayrollItems
            .AsNoTracking()
            .Where(i => i.OrganizationId == organizationId &&
                        i.Status == PayrollItemStatus.Completed &&
                        i.CreatedAtUtc >= targetYearStart && i.CreatedAtUtc < targetYearEnd)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var priorItems = await _dbContext.PayrollItems
            .AsNoTracking()
            .Where(i => i.OrganizationId == organizationId &&
                        i.Status == PayrollItemStatus.Completed &&
                        i.CreatedAtUtc >= priorYearStart && i.CreatedAtUtc < priorYearEnd)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // 1. Spend Metrics
        var targetNgn = targetItems.Where(i => i.Currency == Currency.NGN).Sum(i => i.NetPay);
        var priorNgn = priorItems.Where(i => i.Currency == Currency.NGN).Sum(i => i.NetPay);

        var targetIntNgn = targetItems.Where(i => i.Currency == Currency.INTERNATIONAL_NGN).Sum(i => i.NetPay);
        var priorIntNgn = priorItems.Where(i => i.Currency == Currency.INTERNATIONAL_NGN).Sum(i => i.NetPay);

        var targetUsdt = targetItems.Where(i => i.Currency == Currency.USDT).Sum(i => i.NetPay);
        var priorUsdt = priorItems.Where(i => i.Currency == Currency.USDT).Sum(i => i.NetPay);

        var targetEmployeesPaid = targetItems.Select(i => i.EmployeeUserId).Distinct().Count();
        var priorEmployeesPaid = priorItems.Select(i => i.EmployeeUserId).Distinct().Count();

        static string FormatSpendTrend(decimal target, decimal prior)
        {
            if (prior == 0m && target == 0m)
            {
                return "0.00 compared to prior year";
            }
            if (prior == 0m)
            {
                return "Baseline year — no prior historical data";
            }
            if (target >= prior)
            {
                return $"{(target - prior):N2} more than a year";
            }
            return $"{(prior - target):N2} Less than a year";
        }

        static string FormatEmployeeTrend(int target, int prior)
        {
            if (prior == 0 && target == 0)
            {
                return "0 compared to prior year";
            }
            if (prior == 0)
            {
                return $"+{target} active employees paid this year";
            }
            if (target >= prior)
            {
                return $"+{(target - prior)} compared to last year";
            }
            return $"-{(prior - target)} compared to last year";
        }

        var metrics = new OrgPayrollMetricsDto(
            TotalSpendLocal: new PayrollSpendMetricDto(targetNgn, "NGN", FormatSpendTrend(targetNgn, priorNgn)),
            TotalSpendInternational: new PayrollSpendMetricDto(targetIntNgn, "NGN", FormatSpendTrend(targetIntNgn, priorIntNgn)),
            TotalSpendUsdt: new PayrollSpendMetricDto(targetUsdt, "USDT", FormatSpendTrend(targetUsdt, priorUsdt)),
            TotalEmployeesPaid: new PayrollEmployeeMetricDto(targetEmployeesPaid, FormatEmployeeTrend(targetEmployeesPaid, priorEmployeesPaid)));

        // 2. Breakdown Analytics
        var totalGross = targetItems.Sum(i => i.GrossPay);
        var totalNet = targetItems.Sum(i => i.NetPay);
        var totalDeductions = targetItems.Sum(i => i.TotalDeductions);

        var salaryAllocationPercent = totalGross > 0m ? Math.Round((totalNet / totalGross) * 100m, 1) : 0m;
        var deductionAllocationPercent = totalGross > 0m ? Math.Round((totalDeductions / totalGross) * 100m, 1) : 0m;

        // Department distributions
        var deptIds = targetItems.Where(i => i.DepartmentId.HasValue).Select(i => i.DepartmentId!.Value).Distinct().ToList();
        var departments = await _dbContext.Departments
            .AsNoTracking()
            .Where(d => d.OrganizationId == organizationId && deptIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken)
            .ConfigureAwait(false);

        var deptGroups = targetItems
            .Where(i => i.DepartmentId.HasValue && departments.ContainsKey(i.DepartmentId.Value))
            .GroupBy(i => i.DepartmentId!.Value)
            .Select(g => new
            {
                DepartmentId = g.Key,
                DepartmentName = departments[g.Key],
                TotalNet = g.Sum(i => i.NetPay)
            })
            .OrderByDescending(g => g.TotalNet)
            .ToList();

        string topDeptDescription;
        string deptSpendDescription;
        if (deptGroups.Count > 0 && totalNet > 0m)
        {
            var topDept = deptGroups[0];
            var proportion = Math.Round((topDept.TotalNet / totalNet) * 100m, 1);
            topDeptDescription = $"{topDept.DepartmentName} accounts for {proportion:0.#}% of total compensation disbursements.";
            deptSpendDescription = $"{proportion:0.#}% of your {baseCurrency} payroll this year was allocated to paying out employees in {topDept.DepartmentName}.";
        }
        else
        {
            topDeptDescription = "No departmental compensation disbursements recorded for this period.";
            deptSpendDescription = $"0% of your {baseCurrency} payroll this year was allocated to paying out department employees.";
        }

        // Median monthly payout
        decimal medianMonthlySalary = 0m;
        if (targetItems.Count > 0)
        {
            var sortedNet = targetItems.Select(i => i.NetPay).OrderBy(x => x).ToList();
            int mid = sortedNet.Count / 2;
            medianMonthlySalary = sortedNet.Count % 2 != 0 ? sortedNet[mid] : Math.Round((sortedNet[mid - 1] + sortedNet[mid]) / 2m, 2);
        }
        else
        {
            var levelAmounts = await _dbContext.SalaryLevels
                .AsNoTracking()
                .Where(s => s.OrganizationId == organizationId)
                .Select(s => s.BaseAmount)
                .OrderBy(x => x)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (levelAmounts.Count > 0)
            {
                int mid = levelAmounts.Count / 2;
                medianMonthlySalary = levelAmounts.Count % 2 != 0 ? levelAmounts[mid] : Math.Round((levelAmounts[mid - 1] + levelAmounts[mid]) / 2m, 2);
            }
        }

        var medianSalaryDescription = medianMonthlySalary > 0m
            ? $"The average median salary across active full-time departments is ₦{medianMonthlySalary:N0}."
            : "No salary disbursements recorded for active full-time staff.";

        // Contractor Invoices & Discretionary Bonuses from OperatingExpenses
#pragma warning disable CA1862, CA1304, CA1311
        var contractorExpenses = await _dbContext.OperatingExpenses
            .AsNoTracking()
            .Where(e => e.OrganizationId == organizationId &&
                        e.ExpenseDate >= targetYearStart && e.ExpenseDate < targetYearEnd &&
                        (e.Category == ExpenseCategory.Salaries || e.Description.ToLower().Contains("contractor")))
            .SumAsync(e => (decimal?)e.Amount, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        var bonusExpenses = await _dbContext.OperatingExpenses
            .AsNoTracking()
            .Where(e => e.OrganizationId == organizationId &&
                        e.ExpenseDate >= targetYearStart && e.ExpenseDate < targetYearEnd &&
                        e.Description.ToLower().Contains("bonus"))
            .SumAsync(e => (decimal?)e.Amount, cancellationToken)
            .ConfigureAwait(false) ?? 0m;
#pragma warning restore CA1862, CA1304, CA1311

        var bonusDescription = bonusExpenses > 0m
            ? $"₦{bonusExpenses:N2} in discretionary bonuses was processed in the current calendar year."
            : "Zero discretionary bonuses were processed in the current calendar quarter.";

        var contractorDescription = contractorExpenses > 0m
            ? $"Contractor invoices processed through payroll total ₦{contractorExpenses:N0} this quarter."
            : "Contractor invoices processed through payroll total ₦0.00 this quarter.";

        var generalCards = new List<AnalyticsCardDto>
        {
            new("spend-breakdown", "Payroll spend breakdown", $"{salaryAllocationPercent:0.#}% of your {baseCurrency} payroll this year was allocated to paying out salaries"),
            new("spend-annual", "Average payroll spend by annual", totalNet > 0m
                ? $"Annualized payroll expenditure across completed disbursements for {targetYear} is ₦{totalNet:N2}."
                : $"No payroll disbursements recorded for calendar year {targetYear}."),
            new("spend-dept", "Payroll spend per Department", deptSpendDescription)
        };

        var payrollSpendCards = new List<AnalyticsCardDto>
        {
            new("direct-salaries", "Direct Salaries Allocation", $"{salaryAllocationPercent:0.#}% allocated towards gross direct salaries and basic allowances."),
            new("benefits-tax", "Statutory Taxes & Pension", $"{deductionAllocationPercent:0.#}% allocated towards PAYE, NHF, and statutory employee deductions.")
        };

        var salariesAnalyticsCards = new List<AnalyticsCardDto>
        {
            new("median-salary", "Median Monthly Salary", medianSalaryDescription),
            new("top-earning-dept", "Top Earning Department", topDeptDescription)
        };

        var othersAnalyticsCards = new List<AnalyticsCardDto>
        {
            new("bonus-spend", "Discretionary Bonuses & Stipas", bonusDescription),
            new("contractors", "External Contractor Payouts", contractorDescription)
        };

        var breakdown = new OrgPayrollBreakdownDto(
            General: generalCards,
            PayrollSpend: payrollSpendCards,
            SalariesAnalytics: salariesAnalyticsCards,
            OthersAnalytics: othersAnalyticsCards);

        return new OrgPayrollAnalyticsSummaryDto(
            OrganizationId: organizationId,
            Currency: baseCurrency,
            Metrics: metrics,
            Breakdown: breakdown);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<PayrollBatchDto>> GetBatchesAsync(
        Guid organizationId,
        int pageNumber = 1,
        int pageSize = 20,
        PayrollBatchStatus? status = null,
        DateTime? periodStart = null,
        DateTime? periodEnd = null,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.PayrollBatches
            .AsNoTracking()
            .Where(b => b.OrganizationId == organizationId);

        if (status.HasValue)
        {
            query = query.Where(b => b.Status == status.Value);
        }

        if (periodStart.HasValue)
        {
            query = query.Where(b => b.PeriodStart >= periodStart.Value);
        }

        if (periodEnd.HasValue)
        {
            query = query.Where(b => b.PeriodEnd <= periodEnd.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var batches = await query
            .OrderByDescending(b => b.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new PayrollBatchDto(
                b.Id,
                b.BatchReference,
                b.OrganizationId,
                b.Currency,
                b.Status,
                b.TotalEmployees,
                b.TotalGrossAmount,
                b.TotalDeductionsAmount,
                b.TotalNetAmount,
                b.PeriodStart,
                b.PeriodEnd,
                b.CreatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<PayrollBatchDto>(batches, totalCount, pageNumber, pageSize);
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
