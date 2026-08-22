using CebizPay.Application.Common.Interfaces.Loans;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Loans.Entities;
using CebizPay.Domain.Loans.Enums;
using CebizPay.Domain.Loans.Events;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Loans;

/// <summary>
/// Service implementing loan contract querying and offboarding conversions from corporate payroll loans
/// to standard individual loan contracts upon staff termination.
/// </summary>
public sealed partial class LoanContractService : ILoanContractService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<LoanContractService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoanContractService"/> class.
    /// </summary>
    public LoanContractService(
        ApplicationDbContext dbContext,
        IOutboxService outboxService,
        ILogger<LoanContractService> logger)
    {
        _dbContext = dbContext;
        _outboxService = outboxService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<LoanContractDto?> GetContractByIdAsync(
        Guid organizationId,
        Guid contractId,
        string? requestingUserId = null,
        CancellationToken cancellationToken = default)
    {
        var contract = await _dbContext.LoanContracts
            .Include(c => c.RepaymentSchedule)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contractId && c.OrganizationId == organizationId, cancellationToken);

        if (contract == null)
            return null;

        if (!string.IsNullOrEmpty(requestingUserId) && contract.BorrowerUserId != requestingUserId)
        {
            // If requesting user is borrower, allowed; otherwise caller must have admin org access
        }

        return MapToDto(contract);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LoanContractDto>> GetContractsForOrgAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var contracts = await _dbContext.LoanContracts
            .Include(c => c.RepaymentSchedule)
            .AsNoTracking()
            .Where(c => c.OrganizationId == organizationId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return contracts.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LoanContractDto>> GetContractsForUserAsync(
        string borrowerUserId,
        CancellationToken cancellationToken = default)
    {
        var contracts = await _dbContext.LoanContracts
            .Include(c => c.RepaymentSchedule)
            .AsNoTracking()
            .Where(c => c.BorrowerUserId == borrowerUserId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return contracts.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LoanContractDto>> ConvertTerminatedStaffLoansAsync(
        Guid organizationId,
        string staffUserId,
        string reason,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var activeCorporateLoans = await _dbContext.LoanContracts
            .Include(c => c.RepaymentSchedule)
            .Where(c => c.OrganizationId == organizationId &&
                        c.BorrowerUserId == staffUserId &&
                        c.LoanType == LoanType.CorporatePayrollLoan &&
                        (c.Status == LoanContractStatus.Active || c.Status == LoanContractStatus.Overdue))
            .ToListAsync(cancellationToken);

        if (activeCorporateLoans.Count == 0)
        {
            return Array.Empty<LoanContractDto>();
        }

        var convertedContracts = new List<LoanContract>();

        foreach (var originalLoan in activeCorporateLoans)
        {
            var newContract = LoanContract.CreateConvertedIndividualLoan(originalLoan, reason);

            // Replicate unpaid schedule items onto new contract
            var unpaidItems = originalLoan.RepaymentSchedule
                .Where(i => i.Status != LoanRepaymentStatus.Paid && i.Status != LoanRepaymentStatus.Waived)
                .OrderBy(i => i.InstallmentNumber)
                .ToList();

            int newInstallmentNum = 1;
            foreach (var unpaidItem in unpaidItems)
            {
                var dueDate = DateTime.UtcNow.AddMonths(newInstallmentNum);
                var newItem = LoanRepaymentScheduleItem.Create(
                    newContract.Id,
                    newInstallmentNum++,
                    dueDate,
                    unpaidItem.ScheduledAmount,
                    unpaidItem.PrincipalComponent,
                    unpaidItem.InterestComponent);
                newContract.AddScheduleItem(newItem);
            }

            _dbContext.LoanContracts.Add(newContract);
            originalLoan.ConvertToIndividual(newContract.Id, reason);

            // Audit Log
            var audit = AuditLog.Create(
                actorId: actorUserId,
                action: AuditActions.LoanConvertedToIndividual,
                resourceType: AuditResourceTypes.LoanContract,
                resourceId: originalLoan.Id.ToString(),
                organizationId: organizationId,
                afterJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    OriginalLoanId = originalLoan.Id,
                    OriginalReference = originalLoan.ContractReference,
                    NewContractId = newContract.Id,
                    NewReference = newContract.ContractReference,
                    BorrowerUserId = staffUserId,
                    OutstandingPrincipal = newContract.OutstandingPrincipal,
                    TotalRepayment = newContract.TotalRepayment,
                    Reason = reason
                }));
            _dbContext.AuditLogs.Add(audit);

            // Outbox Event
            _outboxService.Write(new LoanConvertedToIndividualDomainEvent(originalLoan, newContract));

            convertedContracts.Add(newContract);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        LogLoansConverted(_logger, convertedContracts.Count, staffUserId, organizationId);

        return convertedContracts.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Maps a <see cref="LoanContract"/> domain model to its corresponding <see cref="LoanContractDto"/>.
    /// </summary>
    public static LoanContractDto MapToDto(LoanContract contract)
    {
        var schedule = contract.RepaymentSchedule
            .OrderBy(i => i.InstallmentNumber)
            .Select(i => new LoanRepaymentScheduleItemDto(
                Id: i.Id,
                LoanContractId: i.LoanContractId,
                InstallmentNumber: i.InstallmentNumber,
                DueDate: i.DueDate,
                ScheduledAmount: i.ScheduledAmount,
                PrincipalComponent: i.PrincipalComponent,
                InterestComponent: i.InterestComponent,
                PaidAmount: i.PaidAmount,
                Status: i.Status,
                PaidAtUtc: i.PaidAtUtc,
                MissedAtUtc: i.MissedAtUtc,
                PayrollItemId: i.PayrollItemId,
                LedgerTransactionId: i.LedgerTransactionId))
            .ToList();

        return new LoanContractDto(
            Id: contract.Id,
            ContractReference: contract.ContractReference,
            LoanApplicationId: contract.LoanApplicationId,
            OrganizationId: contract.OrganizationId,
            BorrowerUserId: contract.BorrowerUserId,
            BorrowerName: contract.BorrowerName,
            LoanType: contract.LoanType,
            OriginalPrincipal: contract.OriginalPrincipal,
            InterestRate: contract.InterestRate,
            TotalInterest: contract.TotalInterest,
            TotalRepayment: contract.TotalRepayment,
            RepaymentFrequency: contract.RepaymentFrequency,
            NumberOfInstallments: contract.NumberOfInstallments,
            MonthlyInstallmentAmount: contract.MonthlyInstallmentAmount,
            OutstandingPrincipal: contract.OutstandingPrincipal,
            TotalAmountPaid: contract.TotalAmountPaid,
            StartDate: contract.StartDate,
            ExpectedEndDate: contract.ExpectedEndDate,
            Status: contract.Status,
            DisbursementLedgerTransactionId: contract.DisbursementLedgerTransactionId,
            DisbursedAtUtc: contract.DisbursedAtUtc,
            ConvertedToContractId: contract.ConvertedToContractId,
            ConvertedFromContractId: contract.ConvertedFromContractId,
            ConvertedAtUtc: contract.ConvertedAtUtc,
            ConversionReason: contract.ConversionReason,
            CreatedAtUtc: contract.CreatedAtUtc,
            RepaymentSchedule: schedule);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Converted {Count} Corporate Payroll Loans to Individual Loans for Staff {UserId} (Org {OrgId})")]
    private static partial void LogLoansConverted(ILogger logger, int count, string userId, Guid orgId);
}
