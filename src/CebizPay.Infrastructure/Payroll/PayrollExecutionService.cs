using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Payroll;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payroll.Entities;
using CebizPay.Domain.Payroll.Enums;
using CebizPay.Domain.Payroll.Events;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Payroll;

/// <summary>
/// Infrastructure service executing single-item atomic financial disbursements within isolated PostgreSQL database transactions.
/// </summary>
public sealed partial class PayrollExecutionService : IPayrollExecutionService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILedgerPostingService _ledgerPostingService;
    private readonly IOutboxService _outbox;
    private readonly ILogger<PayrollExecutionService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="PayrollExecutionService"/>.
    /// </summary>
    public PayrollExecutionService(
        ApplicationDbContext dbContext,
        ILedgerPostingService ledgerPostingService,
        IOutboxService outbox,
        ILogger<PayrollExecutionService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _ledgerPostingService = ledgerPostingService ?? throw new ArgumentNullException(nameof(ledgerPostingService));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<PayrollItemExecutionResult> ExecutePayrollItemAsync(
        Guid payrollItemId,
        string workerId,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.PayrollItems
            .Include(i => i.Attempts)
            .FirstOrDefaultAsync(i => i.Id == payrollItemId, cancellationToken)
            .ConfigureAwait(false);

        if (item == null)
        {
            return new PayrollItemExecutionResult(false, null, null, "ITEM_NOT_FOUND", $"PayrollItem '{payrollItemId}' not found.");
        }

        // Idempotency check: If already completed, return successful result without re-executing financial movement
        if (item.Status == PayrollItemStatus.Completed && item.LedgerTransactionId.HasValue && item.PaymentVoucherId.HasValue)
        {
            LogPayrollItemIdempotentSkip(_logger, item.Id);
            return new PayrollItemExecutionResult(true, item.LedgerTransactionId, item.PaymentVoucherId, null, null);
        }

        // Begin isolated PostgreSQL transaction for this single item
        await using var dbTx = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 1. Verify batch and organization status
            var batch = await _dbContext.PayrollBatches
                .FirstOrDefaultAsync(b => b.Id == item.PayrollBatchId, cancellationToken)
                .ConfigureAwait(false);

            if (batch == null || batch.Status == PayrollBatchStatus.Cancelled)
            {
                throw new InvalidOperationException($"PayrollBatch '{item.PayrollBatchId}' is missing or cancelled.");
            }

            var org = await _dbContext.Organizations
                .FirstOrDefaultAsync(o => o.Id == item.OrganizationId, cancellationToken)
                .ConfigureAwait(false);

            if (org == null || org.Status == OrganizationStatus.Suspended || org.IsDeleted)
            {
                throw new InvalidOperationException("Organization is suspended or inactive.");
            }

            // 2. Resolve organization wallet in batch currency
            var orgWallet = await _dbContext.Wallets
                .FirstOrDefaultAsync(w => w.OrganizationId == item.OrganizationId && w.Currency == item.Currency, cancellationToken)
                .ConfigureAwait(false);

            if (orgWallet == null)
            {
                throw new InvalidOperationException($"Organization wallet for currency '{item.Currency}' not found.");
            }

            if (orgWallet.Status != WalletStatus.Active)
            {
                throw new InvalidOperationException($"Organization wallet is '{orgWallet.Status}'.");
            }

            // 3. Resolve or create employee personal wallet in batch currency
            var empWallet = await _dbContext.Wallets
                .FirstOrDefaultAsync(w => w.IndividualId == item.EmployeeUserId && w.Currency == item.Currency, cancellationToken)
                .ConfigureAwait(false);

            if (empWallet == null)
            {
                empWallet = Wallet.CreateIndividualWallet(item.EmployeeUserId, item.Currency);
                _dbContext.Wallets.Add(empWallet);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            if (empWallet.Status != WalletStatus.Active)
            {
                throw new InvalidOperationException($"Employee wallet is '{empWallet.Status}'.");
            }

            // 4. Post double-entry disbursement through Central Ledger (Debit Org Wallet, Credit Employee Wallet)
            var reference = $"PRL-{item.Id:N}";
            var description = $"Salary disbursement for {item.EmployeeName} ({item.Currency})";

            var ledgerTxn = await _ledgerPostingService.PostPayrollDisbursementCoreAsync(
                organizationWalletId: orgWallet.Id,
                employeeWalletId: empWallet.Id,
                amount: item.NetPay,
                currency: item.Currency,
                reference: reference,
                description: description,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // 5. Generate immutable Payment Voucher
            var voucher = PaymentVoucher.Create(
                payrollBatchId: batch.Id,
                payrollItemId: item.Id,
                ledgerTransactionId: ledgerTxn.Id,
                organizationId: item.OrganizationId,
                employeeUserId: item.EmployeeUserId,
                employeeName: item.EmployeeName,
                grossPay: item.GrossPay,
                deductions: item.TotalDeductions,
                netPay: item.NetPay,
                currency: item.Currency,
                bankName: "CebizPay Internal Settlement",
                description: $"Payment Voucher for {item.EmployeeName} ({batch.BatchReference})");

            _dbContext.PaymentVouchers.Add(voucher);

            // 6. Settle any attached Corporate Loan Repayment Deductions atomically
            if (!string.IsNullOrWhiteSpace(item.DeductionsDetailJson))
            {
                try
                {
                    var deductionList = JsonSerializer.Deserialize<List<PayrollDeductionDetailDto>>(item.DeductionsDetailJson);
                    if (deductionList != null)
                    {
                        foreach (var deduction in deductionList.Where(d => d.DeductionType == "CORPORATE_LOAN_REPAYMENT" && !string.IsNullOrEmpty(d.Reference)))
                        {
                            if (Guid.TryParse(deduction.Reference, out var installmentId))
                            {
                                var installment = await _dbContext.LoanRepaymentScheduleItems
                                    .FirstOrDefaultAsync(s => s.Id == installmentId, cancellationToken)
                                    .ConfigureAwait(false);

                                if (installment != null && installment.Status != Domain.Loans.Enums.LoanRepaymentStatus.Paid)
                                {
                                    var loanContract = await _dbContext.LoanContracts
                                        .Include(c => c.RepaymentSchedule)
                                        .FirstOrDefaultAsync(c => c.Id == installment.LoanContractId, cancellationToken)
                                        .ConfigureAwait(false);

                                    if (loanContract != null)
                                    {
                                        loanContract.ApplyRepayment(installment.InstallmentNumber, deduction.Amount, item.Id, ledgerTxn.Id);
                                        LogLoanInstallmentSettled(_logger, installment.InstallmentNumber, deduction.Amount, loanContract.Id, item.Id);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogLoanDeductionParseError(_logger, item.Id, ex);
                }
            }

            // 7. Mark PayrollItem Completed
            item.MarkCompleted(ledgerTxn.Id, voucher.Id);

            // 7. Write Audit and Outbox events
            var auditPayload = JsonSerializer.Serialize(new
            {
                BatchReference = batch.BatchReference,
                item.EmployeeUserId,
                item.EmployeeName,
                item.GrossPay,
                item.TotalDeductions,
                item.NetPay,
                item.Currency,
                LedgerTransactionId = ledgerTxn.Id,
                VoucherReference = voucher.VoucherReference
            });

            var audit = AuditLog.Create(
                actorId: workerId,
                action: AuditActions.PayrollItemCompleted,
                resourceType: AuditResourceTypes.PayrollItem,
                resourceId: item.Id.ToString(),
                afterJson: auditPayload,
                organizationId: item.OrganizationId);
            _dbContext.AuditLogs.Add(audit);

            var voucherAudit = AuditLog.Create(
                actorId: workerId,
                action: AuditActions.PaymentVoucherCreated,
                resourceType: AuditResourceTypes.PaymentVoucher,
                resourceId: voucher.Id.ToString(),
                afterJson: auditPayload,
                organizationId: item.OrganizationId);
            _dbContext.AuditLogs.Add(voucherAudit);

            _outbox.Write(new PayrollItemCompletedDomainEvent(
                PayrollBatchId: batch.Id,
                PayrollItemId: item.Id,
                OrganizationId: item.OrganizationId,
                EmployeeUserId: item.EmployeeUserId,
                NetPay: item.NetPay,
                Currency: item.Currency,
                LedgerTransactionId: ledgerTxn.Id,
                PaymentVoucherId: voucher.Id,
                OccurredOnUtc: DateTime.UtcNow));

            _outbox.Write(new PaymentVoucherCreatedDomainEvent(
                PaymentVoucherId: voucher.Id,
                VoucherReference: voucher.VoucherReference,
                PayrollBatchId: batch.Id,
                PayrollItemId: item.Id,
                OrganizationId: item.OrganizationId,
                EmployeeUserId: item.EmployeeUserId,
                NetPay: item.NetPay,
                Currency: item.Currency,
                OccurredOnUtc: DateTime.UtcNow));

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await dbTx.CommitAsync(cancellationToken).ConfigureAwait(false);

            LogPayrollItemCompletedSuccess(_logger, item.Id, item.NetPay, item.Currency, item.EmployeeUserId);
            return new PayrollItemExecutionResult(true, ledgerTxn.Id, voucher.Id, null, null);
        }
        catch (Exception ex)
        {
            await dbTx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            LogPayrollItemExecutionFailure(_logger, item.Id, ex.Message, ex);

            // Record failure in a clean transaction
            var failureCode = ex is InvalidOperationException ? "BUSINESS_RULE_VIOLATION" : "EXECUTION_ERROR";
            var failureReason = ex.Message;

            item.MarkFailed(failureCode, failureReason);

            var failAudit = AuditLog.Create(
                actorId: workerId,
                action: AuditActions.PayrollItemFailed,
                resourceType: AuditResourceTypes.PayrollItem,
                resourceId: item.Id.ToString(),
                afterJson: JsonSerializer.Serialize(new { FailureCode = failureCode, FailureReason = failureReason, Attempt = item.CurrentAttemptNumber }),
                organizationId: item.OrganizationId);
            _dbContext.AuditLogs.Add(failAudit);

            _outbox.Write(new PayrollItemFailedDomainEvent(
                PayrollBatchId: item.PayrollBatchId,
                PayrollItemId: item.Id,
                OrganizationId: item.OrganizationId,
                EmployeeUserId: item.EmployeeUserId,
                FailureCode: failureCode,
                FailureReason: failureReason,
                AttemptNumber: item.CurrentAttemptNumber,
                OccurredOnUtc: DateTime.UtcNow));

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception saveEx)
            {
                LogPayrollItemFailurePersistenceError(_logger, item.Id, saveEx);
            }

            return new PayrollItemExecutionResult(false, null, null, failureCode, failureReason);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "PayrollItem {ItemId} already completed. Idempotently skipped financial execution.")]
    private static partial void LogPayrollItemIdempotentSkip(ILogger logger, Guid itemId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Successfully settled PayrollItem {ItemId} ({Amount} {Currency}) to employee {EmployeeUserId}.")]
    private static partial void LogPayrollItemCompletedSuccess(ILogger logger, Guid itemId, decimal amount, Currency currency, string employeeUserId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed to execute PayrollItem {ItemId}: {ErrorMessage}")]
    private static partial void LogPayrollItemExecutionFailure(ILogger logger, Guid itemId, string errorMessage, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Failed to persist failure state for PayrollItem {ItemId}")]
    private static partial void LogPayrollItemFailurePersistenceError(ILogger logger, Guid itemId, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Settled loan repayment installment #{Num} ({Amount}) for Contract {ContractId} via Payroll Item {ItemId}")]
    private static partial void LogLoanInstallmentSettled(ILogger logger, int num, decimal amount, Guid contractId, Guid itemId);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "Failed to parse deduction details or settle loan schedule item for item {ItemId}")]
    private static partial void LogLoanDeductionParseError(ILogger logger, Guid itemId, Exception exception);
}
