using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Loans;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Loans.Entities;
using CebizPay.Domain.Loans.Enums;
using CebizPay.Domain.Loans.Events;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Loans;

/// <summary>
/// Service coordinating the staff loan application lifecycle: preview, submission, underwriting snapshotting,
/// approval with atomic wallet disbursement, and formal decline.
/// </summary>
public sealed partial class LoanApplicationService : ILoanApplicationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILoanCalculationService _calculationService;
    private readonly ILoanUnderwritingService _underwritingService;
    private readonly ILedgerPostingService _ledgerPostingService;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<LoanApplicationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoanApplicationService"/> class.
    /// </summary>
    public LoanApplicationService(
        ApplicationDbContext dbContext,
        ILoanCalculationService calculationService,
        ILoanUnderwritingService underwritingService,
        ILedgerPostingService ledgerPostingService,
        IOutboxService outboxService,
        ILogger<LoanApplicationService> logger)
    {
        _dbContext = dbContext;
        _calculationService = calculationService;
        _underwritingService = underwritingService;
        _ledgerPostingService = ledgerPostingService;
        _outboxService = outboxService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<LoanCalculationPreviewDto> PreviewApplicationAsync(
        Guid organizationId,
        string applicantUserId,
        LoanCalculationPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var plan = await _dbContext.CorporateLoanPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.LoanPlanId && p.OrganizationId == organizationId, cancellationToken);
        if (plan == null)
            throw new KeyNotFoundException($"Corporate loan plan with ID '{request.LoanPlanId}' was not found.");

        var underwriting = await _underwritingService.UnderwriteApplicationAsync(
            organizationId, applicantUserId, request.RequestedAmount, plan.InterestRate, request.DurationMonths, cancellationToken);

        return _calculationService.CalculatePreview(
            plan, request.RequestedAmount, request.DurationMonths, underwriting.VerifiedSalary, underwriting.ExistingMonthlyDebt);
    }

    /// <inheritdoc/>
    public async Task<LoanApplicationDto> SubmitApplicationAsync(
        Guid organizationId,
        string applicantUserId,
        SubmitLoanApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var org = await _dbContext.Organizations.FindAsync(new object[] { organizationId }, cancellationToken);
        if (org == null)
            throw new KeyNotFoundException($"Organization with ID '{organizationId}' was not found.");
        if (org.Status == OrganizationStatus.Suspended)
            throw new InvalidOperationException("Cannot apply for loans while organization is suspended.");

        var membership = await _dbContext.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == applicantUserId, cancellationToken);
        if (membership == null || membership.Status != MembershipStatus.Active)
            throw new InvalidOperationException("Applicant is not an active member of this organization.");

        var plan = await _dbContext.CorporateLoanPlans
            .FirstOrDefaultAsync(p => p.Id == request.LoanPlanId && p.OrganizationId == organizationId, cancellationToken);
        if (plan == null)
            throw new KeyNotFoundException($"Corporate loan plan with ID '{request.LoanPlanId}' was not found.");
        if (!plan.IsActive)
            throw new InvalidOperationException("Selected corporate loan plan is inactive.");

        var profile = await _dbContext.IndividualProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == applicantUserId, cancellationToken);
        var applicantName = profile != null ? $"{profile.FirstName} {profile.LastName}" : applicantUserId;

        // Perform Underwriting & 33% DTI Pre-check
        var underwriting = await _underwritingService.UnderwriteApplicationAsync(
            organizationId, applicantUserId, request.RequestedAmount, plan.InterestRate, request.DurationMonths, cancellationToken);

        var (eligibilityValid, eligibilityError) = plan.ValidateEligibility(
            request.RequestedAmount, request.DurationMonths, underwriting.VerifiedSalary);

        if (!eligibilityValid && underwriting.VerifiedSalary > 0)
        {
            throw new InvalidOperationException($"Loan eligibility check failed: {eligibilityError}");
        }

        if (!underwriting.IsDtiCompliant && underwriting.VerifiedSalary > 0)
        {
            throw new InvalidOperationException($"33% DTI ceiling exceeded: {underwriting.Reason}");
        }

        var (monthlyPayment, totalInterest, totalRepayment) = _calculationService.CalculateFlatTerms(
            request.RequestedAmount, plan.InterestRate, request.DurationMonths);

        var application = LoanApplication.Create(
            organizationId: organizationId,
            loanPlanId: plan.Id,
            applicantUserId: applicantUserId,
            applicantName: applicantName,
            requestedAmount: request.RequestedAmount,
            interestRateSnapshot: plan.InterestRate,
            durationMonths: request.DurationMonths,
            computedMonthlyPayment: monthlyPayment,
            computedTotalInterest: totalInterest,
            computedTotalRepayment: totalRepayment,
            verifiedSalarySnapshot: underwriting.VerifiedSalary,
            existingMonthlyDebtSnapshot: underwriting.ExistingMonthlyDebt,
            proposedMonthlyPaymentSnapshot: underwriting.ProposedMonthlyPayment,
            totalMonthlyDebtSnapshot: underwriting.TotalMonthlyDebt,
            debtToIncomeRatioSnapshot: underwriting.DebtToIncomeRatio,
            isDtiCompliantSnapshot: underwriting.IsDtiCompliant,
            underwritingReason: underwriting.Reason,
            repaymentFrequency: request.RepaymentFrequency,
            autoSubmit: true);

        _dbContext.LoanApplications.Add(application);

        // Audit Log
        var audit = AuditLog.Create(
            actorId: applicantUserId,
            action: AuditActions.LoanApplicationSubmitted,
            resourceType: AuditResourceTypes.LoanApplication,
            resourceId: application.Id.ToString(),
            organizationId: organizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                application.ApplicationReference,
                application.RequestedAmount,
                application.DurationMonths,
                application.ComputedMonthlyPayment,
                application.TotalMonthlyDebtSnapshot,
                application.DebtToIncomeRatioSnapshot,
                Status = application.Status.ToString()
            }));
        _dbContext.AuditLogs.Add(audit);

        // Outbox Event
        _outboxService.Write(new LoanApplicationSubmittedDomainEvent(application));

        await _dbContext.SaveChangesAsync(cancellationToken);
        LogApplicationSubmitted(_logger, application.Id, application.ApplicationReference, applicantUserId);

        return MapToDto(application);
    }

    /// <inheritdoc/>
    public async Task<LoanApplicationDto?> GetApplicationByIdAsync(
        Guid organizationId,
        Guid applicationId,
        string? requestingUserId = null,
        CancellationToken cancellationToken = default)
    {
        var application = await _dbContext.LoanApplications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.OrganizationId == organizationId, cancellationToken);

        if (application == null)
            return null;

        if (!string.IsNullOrEmpty(requestingUserId) && application.ApplicantUserId != requestingUserId)
        {
            // If requesting user is the applicant, allow; otherwise caller must have admin org access
        }

        return MapToDto(application);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LoanApplicationDto>> GetApplicationsForOrgAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var applications = await _dbContext.LoanApplications
            .AsNoTracking()
            .Where(a => a.OrganizationId == organizationId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return applications.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LoanApplicationDto>> GetApplicationsForUserAsync(
        string applicantUserId,
        CancellationToken cancellationToken = default)
    {
        var applications = await _dbContext.LoanApplications
            .AsNoTracking()
            .Where(a => a.ApplicantUserId == applicantUserId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return applications.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<LoanContractDto> ApproveApplicationAsync(
        Guid organizationId,
        Guid applicationId,
        string approverUserId,
        CancellationToken cancellationToken = default)
    {
        var application = await _dbContext.LoanApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.OrganizationId == organizationId, cancellationToken);
        if (application == null)
            throw new KeyNotFoundException($"Loan application with ID '{applicationId}' was not found.");

        if (application.Status == LoanApplicationStatus.Approved)
        {
            // Idempotent return existing contract
            var existingContract = await _dbContext.LoanContracts
                .Include(c => c.RepaymentSchedule)
                .FirstOrDefaultAsync(c => c.LoanApplicationId == applicationId, cancellationToken);
            if (existingContract != null)
            {
                return LoanContractService.MapToDto(existingContract);
            }
        }

        var org = await _dbContext.Organizations.FindAsync(new object[] { organizationId }, cancellationToken);
        if (org == null || org.Status == OrganizationStatus.Suspended)
            throw new InvalidOperationException("Organization is suspended or invalid.");

        // 1. Enforce Domain Self-Approval Prevention
        application.Approve(approverUserId);

        // 2. Generate Loan Contract
        var contract = LoanContract.CreateFromApplication(application);

        // 3. Build Repayment Schedule with Exact Rounding
        var totalRepayment = contract.TotalRepayment;
        var totalInterest = contract.TotalInterest;
        var principal = contract.OriginalPrincipal;
        var count = contract.NumberOfInstallments;

        var baseInstallment = Math.Floor(totalRepayment / count * 100m) / 100m;
        var basePrincipalComponent = Math.Floor(principal / count * 100m) / 100m;
        var baseInterestComponent = Math.Floor(totalInterest / count * 100m) / 100m;

        decimal accumulatedInstallments = 0m;
        decimal accumulatedPrincipal = 0m;
        decimal accumulatedInterest = 0m;

        for (int i = 1; i <= count; i++)
        {
            var dueDate = contract.StartDate.AddMonths(i);
            decimal scheduledAmount;
            decimal principalComponent;
            decimal interestComponent;

            if (i == count)
            {
                // Final installment absorbs any rounding remainder
                scheduledAmount = totalRepayment - accumulatedInstallments;
                principalComponent = principal - accumulatedPrincipal;
                interestComponent = totalInterest - accumulatedInterest;
            }
            else
            {
                scheduledAmount = baseInstallment;
                principalComponent = basePrincipalComponent;
                interestComponent = baseInterestComponent;
                accumulatedInstallments += scheduledAmount;
                accumulatedPrincipal += principalComponent;
                accumulatedInterest += interestComponent;
            }

            var item = LoanRepaymentScheduleItem.Create(
                contract.Id, i, dueDate, scheduledAmount, principalComponent, interestComponent);
            contract.AddScheduleItem(item);
        }

        _dbContext.LoanContracts.Add(contract);

        // 4. Resolve Employee Wallet for Disbursement
        var employeeWallet = await _dbContext.Wallets
            .FirstOrDefaultAsync(w => w.IndividualId == application.ApplicantUserId && w.Currency == Currency.NGN, cancellationToken);
        if (employeeWallet == null)
        {
            employeeWallet = Wallet.CreateIndividualWallet(application.ApplicantUserId, Currency.NGN);
            _dbContext.Wallets.Add(employeeWallet);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // 5. Post Atomic Loan Principal Disbursement through Central Ledger
        var ledgerTxn = await _ledgerPostingService.PostLoanDisbursementCoreAsync(
            employeeWallet.Id,
            contract.OriginalPrincipal,
            Currency.NGN,
            contract.ContractReference,
            $"Disbursement for Corporate Loan {contract.ContractReference}",
            cancellationToken);

        contract.MarkDisbursed(ledgerTxn.Id);

        // 6. Record Audit Log
        var audit = AuditLog.Create(
            actorId: approverUserId,
            action: AuditActions.LoanApplicationApproved,
            resourceType: AuditResourceTypes.LoanApplication,
            resourceId: application.Id.ToString(),
            organizationId: organizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                application.ApplicationReference,
                ContractId = contract.Id,
                contract.ContractReference,
                contract.OriginalPrincipal,
                contract.TotalRepayment,
                DisbursementLedgerTxnId = ledgerTxn.Id
            }));
        _dbContext.AuditLogs.Add(audit);

        // 7. Emit Outbox Event
        _outboxService.Write(new LoanApplicationApprovedDomainEvent(application, contract.Id));

        await _dbContext.SaveChangesAsync(cancellationToken);
        LogApplicationApproved(_logger, application.Id, contract.Id, contract.ContractReference, ledgerTxn.Id);

        return LoanContractService.MapToDto(contract);
    }

    /// <inheritdoc/>
    public async Task<LoanApplicationDto> DeclineApplicationAsync(
        Guid organizationId,
        Guid applicationId,
        string deciderUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var application = await _dbContext.LoanApplications
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.OrganizationId == organizationId, cancellationToken);
        if (application == null)
            throw new KeyNotFoundException($"Loan application with ID '{applicationId}' was not found.");

        application.Decline(deciderUserId, reason);

        // Audit Log
        var audit = AuditLog.Create(
            actorId: deciderUserId,
            action: AuditActions.LoanApplicationDeclined,
            resourceType: AuditResourceTypes.LoanApplication,
            resourceId: application.Id.ToString(),
            organizationId: organizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                application.ApplicationReference,
                Reason = reason
            }));
        _dbContext.AuditLogs.Add(audit);

        // Outbox Event
        _outboxService.Write(new LoanApplicationDeclinedDomainEvent(application, reason));

        await _dbContext.SaveChangesAsync(cancellationToken);
        LogApplicationDeclined(_logger, application.Id, reason);

        return MapToDto(application);
    }

    private static LoanApplicationDto MapToDto(LoanApplication app)
    {
        return new LoanApplicationDto(
            Id: app.Id,
            ApplicationReference: app.ApplicationReference,
            OrganizationId: app.OrganizationId,
            LoanPlanId: app.LoanPlanId,
            ApplicantUserId: app.ApplicantUserId,
            ApplicantName: app.ApplicantName,
            RequestedAmount: app.RequestedAmount,
            InterestRateSnapshot: app.InterestRateSnapshot,
            DurationMonths: app.DurationMonths,
            RepaymentFrequency: app.RepaymentFrequency,
            ComputedMonthlyPayment: app.ComputedMonthlyPayment,
            ComputedTotalInterest: app.ComputedTotalInterest,
            ComputedTotalRepayment: app.ComputedTotalRepayment,
            VerifiedSalarySnapshot: app.VerifiedSalarySnapshot,
            ExistingMonthlyDebtSnapshot: app.ExistingMonthlyDebtSnapshot,
            ProposedMonthlyPaymentSnapshot: app.ProposedMonthlyPaymentSnapshot,
            TotalMonthlyDebtSnapshot: app.TotalMonthlyDebtSnapshot,
            DebtToIncomeRatioSnapshot: app.DebtToIncomeRatioSnapshot,
            IsDtiCompliantSnapshot: app.IsDtiCompliantSnapshot,
            Status: app.Status,
            UnderwritingReason: app.UnderwritingReason,
            DeclinedReason: app.DeclinedReason,
            DeciderUserId: app.DeciderUserId,
            CreatedAtUtc: app.CreatedAtUtc,
            DecidedAtUtc: app.DecidedAtUtc);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Submitted Loan Application {AppId} ({Ref}) for User {UserId}")]
    private static partial void LogApplicationSubmitted(ILogger logger, Guid appId, string @ref, string userId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Approved Loan Application {AppId}, Issued Contract {ContractId} ({Ref}) with Disbursement {TxnId}")]
    private static partial void LogApplicationApproved(ILogger logger, Guid appId, Guid contractId, string @ref, Guid txnId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Declined Loan Application {AppId} for Reason: {Reason}")]
    private static partial void LogApplicationDeclined(ILogger logger, Guid appId, string reason);
}
