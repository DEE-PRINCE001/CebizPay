using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Savings;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Savings.Entities;
using CebizPay.Domain.Savings.Enums;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Savings;

/// <summary>
/// Infrastructure service implementation for savings plans, account lifecycle, contributions, interest accrual, and withdrawals.
/// </summary>
public class SavingsService : ISavingsService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILedgerPostingService _ledgerPostingService;
    private readonly ISavingsInterestPolicyService _policyService;

    /// <summary>
    /// Initializes a new instance of SavingsService.
    /// </summary>
    public SavingsService(
        IApplicationDbContext dbContext,
        ILedgerPostingService ledgerPostingService,
        ISavingsInterestPolicyService policyService)
    {
        _dbContext = dbContext;
        _ledgerPostingService = ledgerPostingService;
        _policyService = policyService;
    }

    /// <inheritdoc/>
    public async Task<SavingsPreviewResult> PreviewSavingsAsync(SavingsPreviewRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(request));
        if (request.DurationDays < 30 && request.PlanType == SavingsPlanType.FixedLock)
            throw new ArgumentException("Fixed-lock duration must be at least 30 days.", nameof(request));
        if (request.DurationDays > 730 && request.PlanType == SavingsPlanType.FixedLock)
            throw new ArgumentException("Fixed-lock duration cannot exceed 730 days (2 years).", nameof(request));

        // Get applicable interest rate from policy or defaults
        var activePolicy = await _policyService.GetActivePolicyAsync(request.PlanType, cancellationToken);
        var annualRate = activePolicy?.AnnualRate ?? (request.PlanType == SavingsPlanType.FixedLock ? 0.10m : 0m);

        var dailyRate = annualRate / 365m;
        var totalInterest = Math.Round(request.Amount * dailyRate * request.DurationDays, 2, MidpointRounding.AwayFromZero);
        var maturityPayout = request.Amount + totalInterest;

        var penaltyRate = request.PlanType == SavingsPlanType.FixedLock ? 0.025m : 0m;
        var penaltyAmount = Math.Round(request.Amount * penaltyRate, 2, MidpointRounding.AwayFromZero);
        var earlyNetPayout = request.Amount - penaltyAmount;

        return new SavingsPreviewResult(
            request.PlanType,
            request.Amount,
            request.DurationDays,
            annualRate,
            totalInterest,
            maturityPayout,
            penaltyRate,
            penaltyAmount,
            earlyNetPayout);
    }

    /// <inheritdoc/>
    public async Task<SavingsPlanDto> CreatePlanAsync(string createdByUserId, CreateSavingsPlanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(createdByUserId))
            throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));

        var activePolicy = await _policyService.GetActivePolicyAsync(request.PlanType, cancellationToken);
        var policyVersion = activePolicy?.Version ?? 1;

        SavingsPlan plan;
        if (request.PlanType == SavingsPlanType.FixedLock)
        {
            plan = SavingsPlan.CreateFixedLockPlan(
                request.OrganizationId,
                createdByUserId,
                request.OwnerType,
                request.Name,
                request.Description,
                request.Currency,
                request.InterestRate,
                request.MinimumAmount,
                request.MaximumAmount,
                request.MinimumDurationDays,
                request.MaximumDurationDays,
                policyVersion);
        }
        else
        {
            plan = SavingsPlan.CreateGoalBasedPlan(
                request.OrganizationId,
                createdByUserId,
                request.OwnerType,
                request.Name,
                request.Description,
                request.Currency,
                request.TargetAmount ?? request.MaximumAmount,
                request.ContributionAmount ?? request.MinimumAmount,
                request.ContributionFrequency ?? SavingsContributionFrequency.Monthly,
                request.InterestRate,
                policyVersion);
        }

        _dbContext.SavingsPlans.Add(plan);

        var audit = AuditLog.Create(
            actorId: createdByUserId,
            action: AuditActions.SavingsPlanCreated,
            resourceType: AuditResourceTypes.SavingsPlan,
            resourceId: plan.Id.ToString(),
            organizationId: plan.OrganizationId,
            afterJson: $"{{\"name\":\"{plan.Name}\",\"planType\":\"{plan.PlanType}\",\"interestRate\":{plan.InterestRate}}}");
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapPlanToDto(plan);
    }

    /// <inheritdoc/>
    public async Task<SavingsPlanDto?> GetPlanByIdAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var plan = await _dbContext.SavingsPlans.FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);
        return plan == null ? null : MapPlanToDto(plan);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SavingsPlanDto>> GetAvailablePlansAsync(Guid? organizationId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SavingsPlans.Where(p => p.IsActive);
        if (organizationId.HasValue)
        {
            query = query.Where(p => p.OrganizationId == organizationId.Value || p.OwnerType == SavingsOwnerType.Individual);
        }
        else
        {
            query = query.Where(p => p.OwnerType == SavingsOwnerType.Individual);
        }

        var plans = await query.OrderBy(p => p.Name).ToListAsync(cancellationToken);
        return plans.Select(MapPlanToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<SavingsAccountDto> OpenAccountAsync(
        string ownerUserId,
        OpenSavingsAccountRequest request,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(ownerUserId))
            throw new ArgumentException("OwnerUserId is required.", nameof(ownerUserId));

        var plan = await _dbContext.SavingsPlans.FirstOrDefaultAsync(p => p.Id == request.SavingsPlanId, cancellationToken)
            ?? throw new InvalidOperationException($"Savings plan '{request.SavingsPlanId}' not found.");

        if (!plan.IsActive)
            throw new InvalidOperationException("Savings plan is not active.");

        if (request.InitialDepositAmount < plan.MinimumAmount)
            throw new ArgumentException($"Initial deposit amount ({request.InitialDepositAmount:F2}) is below plan minimum ({plan.MinimumAmount:F2}).", nameof(request));

        // Resolve user wallet
        var userWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.IndividualId == ownerUserId && w.Currency == plan.Currency, cancellationToken)
            ?? throw new InvalidOperationException($"User wallet not found for currency '{plan.Currency}'.");

        if (userWallet.AvailableBalance < request.InitialDepositAmount)
            throw new InvalidOperationException($"Insufficient wallet balance. Required: {request.InitialDepositAmount:F2}, Available: {userWallet.AvailableBalance:F2}.");

        var nowUtc = DateTime.UtcNow;
        SavingsAccount account;
        if (plan.PlanType == SavingsPlanType.FixedLock)
        {
            account = SavingsAccount.CreateFixedLockAccount(
                plan.Id,
                ownerUserId,
                request.OrganizationId,
                plan.Currency,
                plan.InterestRate,
                plan.InterestPolicyVersion,
                request.DurationDays,
                nowUtc);
        }
        else
        {
            account = SavingsAccount.CreateGoalBasedAccount(
                plan.Id,
                ownerUserId,
                request.OrganizationId,
                plan.Currency,
                request.TargetAmount ?? plan.TargetAmount ?? 0m,
                request.ContributionAmount ?? plan.ContributionAmount ?? 0m,
                request.ContributionFrequency ?? plan.ContributionFrequency ?? SavingsContributionFrequency.Monthly,
                plan.InterestRate,
                plan.InterestPolicyVersion,
                nowUtc,
                nowUtc.AddDays(request.DurationDays));
        }

        _dbContext.SavingsAccounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Execute initial deposit via Central Double-Entry Ledger
        var reference = $"SD-{Guid.NewGuid():N}"[..32];
        var ledgerTx = await _ledgerPostingService.PostSavingsContributionCoreAsync(
            userWallet.Id,
            request.InitialDepositAmount,
            plan.Currency,
            reference,
            $"Initial deposit for savings account {account.Id}",
            cancellationToken);

        // Record contribution and activate account
        var contribution = account.RecordContribution(request.InitialDepositAmount, ledgerTx.Id, idempotencyKey ?? reference);
        _dbContext.SavingsContributions.Add(contribution);

        var audit = AuditLog.Create(
            actorId: ownerUserId,
            action: AuditActions.SavingsAccountCreated,
            resourceType: AuditResourceTypes.SavingsAccount,
            resourceId: account.Id.ToString(),
            organizationId: account.OrganizationId,
            afterJson: $"{{\"initialDeposit\":{request.InitialDepositAmount},\"currency\":\"{plan.Currency}\",\"planType\":\"{account.PlanType}\"}}");
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapAccountToDto(account);
    }

    /// <inheritdoc/>
    public async Task<SavingsAccountDto?> GetAccountByIdAsync(Guid accountId, string requesterUserId, Guid? organizationId = null, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.SavingsAccounts
            .Include(a => a.Contributions)
            .Include(a => a.InterestAccruals)
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account == null)
            return null;

        // Tenant / user isolation check
        if (account.OwnerUserId != requesterUserId && (!organizationId.HasValue || account.OrganizationId != organizationId))
        {
            throw new UnauthorizedAccessException("You are not authorized to view this savings account.");
        }

        return MapAccountToDto(account);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SavingsAccountDto>> GetAccountsAsync(string? ownerUserId = null, Guid? organizationId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SavingsAccounts.AsQueryable();
        if (!string.IsNullOrWhiteSpace(ownerUserId))
            query = query.Where(a => a.OwnerUserId == ownerUserId);
        if (organizationId.HasValue)
            query = query.Where(a => a.OrganizationId == organizationId.Value);

        var accounts = await query.OrderByDescending(a => a.CreatedAtUtc).ToListAsync(cancellationToken);
        return accounts.Select(MapAccountToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<SavingsAccountDto> ContributeAsync(
        Guid accountId,
        string ownerUserId,
        decimal amount,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new ArgumentException("Contribution amount must be positive.", nameof(amount));

        var account = await _dbContext.SavingsAccounts
            .Include(a => a.Contributions)
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new InvalidOperationException($"Savings account '{accountId}' not found.");

        if (account.OwnerUserId != ownerUserId)
            throw new UnauthorizedAccessException("You can only contribute to your own savings account.");

        var userWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.IndividualId == ownerUserId && w.Currency == account.Currency, cancellationToken)
            ?? throw new InvalidOperationException($"User wallet not found for currency '{account.Currency}'.");

        var reference = $"SC-{Guid.NewGuid():N}"[..32];
        var ledgerTx = await _ledgerPostingService.PostSavingsContributionCoreAsync(
            userWallet.Id,
            amount,
            account.Currency,
            reference,
            $"Contribution to savings account {account.Id}",
            cancellationToken);

        var contribution = account.RecordContribution(amount, ledgerTx.Id, idempotencyKey ?? reference);
        _dbContext.SavingsContributions.Add(contribution);

        var audit = AuditLog.Create(
            actorId: ownerUserId,
            action: AuditActions.SavingsContributionMade,
            resourceType: AuditResourceTypes.SavingsAccount,
            resourceId: account.Id.ToString(),
            organizationId: account.OrganizationId,
            afterJson: $"{{\"amount\":{amount},\"currency\":\"{account.Currency}\"}}");
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapAccountToDto(account);
    }

    /// <inheritdoc/>
    public async Task<SavingsPreviewResult> PreviewWithdrawalAsync(Guid accountId, string ownerUserId, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.SavingsAccounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new InvalidOperationException($"Savings account '{accountId}' not found.");

        if (account.OwnerUserId != ownerUserId)
            throw new UnauthorizedAccessException("You can only view withdrawal terms for your own savings account.");

        var terms = account.CalculateWithdrawalTerms(DateTime.UtcNow);
        var durationDays = (int)(DateTime.UtcNow - account.StartDateUtc).TotalDays;

        return new SavingsPreviewResult(
            account.PlanType,
            account.PrincipalBalance,
            Math.Max(1, durationDays),
            account.InterestRateSnapshot,
            account.AccruedInterest,
            terms.PayoutAmount,
            account.PenaltyRateSnapshot,
            terms.PenaltyAmount,
            terms.PayoutAmount);
    }

    /// <inheritdoc/>
    public async Task<SavingsWithdrawalResultDto> WithdrawAsync(
        Guid accountId,
        string ownerUserId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.SavingsAccounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new InvalidOperationException($"Savings account '{accountId}' not found.");

        if (account.OwnerUserId != ownerUserId)
            throw new UnauthorizedAccessException("You can only withdraw from your own savings account.");

        var userWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.IndividualId == ownerUserId && w.Currency == account.Currency, cancellationToken)
            ?? throw new InvalidOperationException($"User wallet not found for currency '{account.Currency}'.");

        var nowUtc = DateTime.UtcNow;
        var terms = account.CalculateWithdrawalTerms(nowUtc);

        var reference = $"SW-{Guid.NewGuid():N}"[..32];
        var ledgerTx = await _ledgerPostingService.PostSavingsWithdrawalCoreAsync(
            userWallet.Id,
            terms.PayoutAmount,
            account.Currency,
            reference,
            $"Withdrawal liquidation for savings account {account.Id}",
            cancellationToken);

        account.ExecuteWithdrawal(terms.PayoutAmount, terms.PenaltyAmount, terms.ForfeitedInterest, ledgerTx.Id, nowUtc);

        var auditAction = terms.IsEarly ? AuditActions.SavingsEarlyWithdrawal : AuditActions.SavingsWithdrawal;
        var audit = AuditLog.Create(
            actorId: ownerUserId,
            action: auditAction,
            resourceType: AuditResourceTypes.SavingsAccount,
            resourceId: account.Id.ToString(),
            organizationId: account.OrganizationId,
            afterJson: $"{{\"payout\":{terms.PayoutAmount},\"penalty\":{terms.PenaltyAmount},\"forfeitedInterest\":{terms.ForfeitedInterest},\"isEarly\":{terms.IsEarly}}}");
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SavingsWithdrawalResultDto(
            account.Id,
            terms.PayoutAmount,
            terms.PenaltyAmount,
            terms.ForfeitedInterest,
            terms.IsEarly,
            ledgerTx.Id,
            nowUtc);
    }

    /// <inheritdoc/>
    public async Task<int> ProcessDailyInterestAccrualAsync(DateTime accrualDate, CancellationToken cancellationToken = default)
    {
        var activeAccounts = await _dbContext.SavingsAccounts
            .Include(a => a.InterestAccruals)
            .Where(a => a.Status == SavingsAccountStatus.Active && a.PrincipalBalance > 0)
            .ToListAsync(cancellationToken);

        var count = 0;
        var dateOnly = accrualDate.Date;

        foreach (var account in activeAccounts)
        {
            // Repeat-safe idempotency check: already accrued for this date?
            var alreadyAccrued = account.InterestAccruals.Any(i => i.AccrualDate == dateOnly);
            if (alreadyAccrued)
                continue;

            if (account.InterestRateSnapshot > 0)
            {
                var dailyInterest = Math.Round(account.PrincipalBalance * (account.InterestRateSnapshot / 365m), 4, MidpointRounding.AwayFromZero);
                if (dailyInterest > 0)
                {
                    var accrual = account.AccrueDailyInterest(dailyInterest, dateOnly);
                    if (accrual != null)
                    {
                        _dbContext.SavingsInterestAccruals.Add(accrual);
                        count++;
                    }
                }
            }

            // Check if maturity reached
            account.CheckMaturity(DateTime.UtcNow);
        }

        if (count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return count;
    }

    private static SavingsPlanDto MapPlanToDto(SavingsPlan plan) =>
        new(
            plan.Id,
            plan.OrganizationId,
            plan.CreatedByUserId,
            plan.OwnerType,
            plan.PlanType,
            plan.Name,
            plan.Description,
            plan.Currency,
            plan.InterestRate,
            plan.MinimumAmount,
            plan.MaximumAmount,
            plan.MinimumDurationDays,
            plan.MaximumDurationDays,
            plan.TargetAmount,
            plan.ContributionAmount,
            plan.ContributionFrequency,
            plan.InterestPolicyVersion,
            plan.IsActive,
            plan.CreatedAtUtc);

    private static SavingsAccountDto MapAccountToDto(SavingsAccount account) =>
        new(
            account.Id,
            account.SavingsPlanId,
            account.OwnerUserId,
            account.OrganizationId,
            account.Currency,
            account.PlanType,
            account.PrincipalBalance,
            account.AccruedInterest,
            account.TotalInterestWithdrawn,
            account.Status,
            account.InterestRateSnapshot,
            account.InterestPolicyVersionSnapshot,
            account.PenaltyRateSnapshot,
            account.TargetAmount,
            account.ContributionAmount,
            account.ContributionFrequency,
            account.StartDateUtc,
            account.MaturityDateUtc,
            account.MaturedAtUtc,
            account.WithdrawnAtUtc,
            account.CreatedAtUtc);
}
