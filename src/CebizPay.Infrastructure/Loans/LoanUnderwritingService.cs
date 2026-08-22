using CebizPay.Application.Common.Interfaces.Loans;
using CebizPay.Domain.Loans.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Loans;

/// <summary>
/// Deterministic loan underwriting service enforcing the non-negotiable 33% Debt-To-Income (DTI) rule.
/// Aggregates existing debt obligations across all active corporate and individual loans.
/// </summary>
public sealed partial class LoanUnderwritingService : ILoanUnderwritingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILoanCalculationService _calculationService;
    private readonly ILogger<LoanUnderwritingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoanUnderwritingService"/> class.
    /// </summary>
    public LoanUnderwritingService(
        ApplicationDbContext dbContext,
        ILoanCalculationService calculationService,
        ILogger<LoanUnderwritingService> logger)
    {
        _dbContext = dbContext;
        _calculationService = calculationService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UnderwritingEvaluationResult> UnderwriteApplicationAsync(
        Guid organizationId,
        string applicantUserId,
        decimal requestedAmount,
        decimal annualInterestRate,
        int durationMonths,
        CancellationToken cancellationToken = default)
    {
        // 1. Resolve verified base salary from active organization membership & assigned salary level
        var membership = await _dbContext.OrganizationMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == applicantUserId, cancellationToken);

        decimal verifiedSalary = 0m;
        if (membership?.SalaryLevelId != null)
        {
            var salaryLevel = await _dbContext.SalaryLevels
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == membership.SalaryLevelId, cancellationToken);
            if (salaryLevel != null)
            {
                verifiedSalary = salaryLevel.BaseAmount;
            }
        }

        // 2. Aggregate existing monthly debt across all active and overdue loans for applicant
        var existingMonthlyDebt = await _dbContext.LoanContracts
            .AsNoTracking()
            .Where(c => c.BorrowerUserId == applicantUserId &&
                        (c.Status == LoanContractStatus.Active || c.Status == LoanContractStatus.Overdue))
            .SumAsync(c => c.MonthlyInstallmentAmount, cancellationToken);

        // 3. Compute proposed monthly installment
        var (proposedMonthlyPayment, _, _) = _calculationService.CalculateFlatTerms(
            requestedAmount, annualInterestRate, durationMonths);

        var totalMonthlyDebt = existingMonthlyDebt + proposedMonthlyPayment;
        var maxAllowedDebt = Math.Round(0.33m * verifiedSalary, 2, MidpointRounding.AwayFromZero);
        var dtiRatio = verifiedSalary > 0 ? Math.Round(totalMonthlyDebt / verifiedSalary, 4, MidpointRounding.AwayFromZero) : 1.0m;
        var isDtiCompliant = totalMonthlyDebt <= maxAllowedDebt && verifiedSalary > 0;

        string? reason = null;
        if (verifiedSalary <= 0)
        {
            reason = "Staff base salary could not be verified from assigned workforce salary level structure. Routed for manual HR review.";
            LogUnverifiableSalary(_logger, applicantUserId, organizationId);
        }
        else if (!isDtiCompliant)
        {
            reason = $"Total monthly debt obligation ({totalMonthlyDebt:N2}) exceeds the 33% debt-to-income ceiling ({maxAllowedDebt:N2}) on verified salary ({verifiedSalary:N2}).";
            LogDtiRejected(_logger, applicantUserId, dtiRatio);
        }

        var isEligible = isDtiCompliant && verifiedSalary > 0;

        return new UnderwritingEvaluationResult(
            Eligible: isEligible,
            VerifiedSalary: verifiedSalary,
            ExistingMonthlyDebt: existingMonthlyDebt,
            ProposedMonthlyPayment: proposedMonthlyPayment,
            TotalMonthlyDebt: totalMonthlyDebt,
            MaxAllowedDebt: maxAllowedDebt,
            DebtToIncomeRatio: dtiRatio,
            IsDtiCompliant: isDtiCompliant,
            Reason: reason);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Underwriting flagged unverifiable salary for User {UserId} in Org {OrgId}")]
    private static partial void LogUnverifiableSalary(ILogger logger, string userId, Guid orgId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Underwriting rejected DTI violation for User {UserId}: DTI={Dti:P1}, Limit=33%")]
    private static partial void LogDtiRejected(ILogger logger, string userId, decimal dti);
}
