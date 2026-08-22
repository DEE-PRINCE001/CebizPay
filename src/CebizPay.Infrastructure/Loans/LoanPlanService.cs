using CebizPay.Application.Common.Interfaces.Loans;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Loans.Entities;
using CebizPay.Domain.Loans.Events;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Loans;

/// <summary>
/// Service implementing corporate loan plan management with tenant isolation, audit trails, and outbox event dispatch.
/// </summary>
public sealed partial class LoanPlanService : ILoanPlanService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<LoanPlanService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoanPlanService"/> class.
    /// </summary>
    public LoanPlanService(
        ApplicationDbContext dbContext,
        IOutboxService outboxService,
        ILogger<LoanPlanService> logger)
    {
        _dbContext = dbContext;
        _outboxService = outboxService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CorporateLoanPlanDto> CreatePlanAsync(
        Guid organizationId,
        CreateLoanPlanRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var org = await _dbContext.Organizations.FindAsync(new object[] { organizationId }, cancellationToken);
        if (org == null)
            throw new KeyNotFoundException($"Organization with ID '{organizationId}' was not found.");

        var plan = CorporateLoanPlan.Create(
            organizationId: organizationId,
            name: request.Name,
            description: request.Description,
            minimumAmount: request.MinimumAmount,
            maximumAmount: request.MaximumAmount,
            interestRate: request.InterestRate,
            minimumDurationMonths: request.MinimumDurationMonths,
            maximumDurationMonths: request.MaximumDurationMonths,
            minimumMonthlySalary: request.MinimumMonthlySalary,
            repaymentFrequency: request.RepaymentFrequency);

        _dbContext.CorporateLoanPlans.Add(plan);

        // Audit Log
        var audit = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.LoanPlanCreated,
            resourceType: AuditResourceTypes.LoanPlan,
            resourceId: plan.Id.ToString(),
            organizationId: organizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                plan.Name,
                plan.MinimumAmount,
                plan.MaximumAmount,
                plan.InterestRate,
                plan.MinimumDurationMonths,
                plan.MaximumDurationMonths,
                plan.MinimumMonthlySalary
            }));
        _dbContext.AuditLogs.Add(audit);

        // Outbox Event
        _outboxService.Write(new LoanPlanCreatedDomainEvent(plan));

        await _dbContext.SaveChangesAsync(cancellationToken);
        LogPlanCreated(_logger, plan.Id, organizationId);

        return MapToDto(plan);
    }

    /// <inheritdoc/>
    public async Task<CorporateLoanPlanDto> UpdatePlanAsync(
        Guid organizationId,
        Guid planId,
        UpdateLoanPlanRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _dbContext.CorporateLoanPlans
            .FirstOrDefaultAsync(p => p.Id == planId && p.OrganizationId == organizationId, cancellationToken);
        if (plan == null)
            throw new KeyNotFoundException($"Corporate loan plan with ID '{planId}' was not found for organization '{organizationId}'.");

        plan.UpdateDetails(
            name: request.Name,
            description: request.Description,
            minimumAmount: request.MinimumAmount,
            maximumAmount: request.MaximumAmount,
            interestRate: request.InterestRate,
            minimumDurationMonths: request.MinimumDurationMonths,
            maximumDurationMonths: request.MaximumDurationMonths,
            minimumMonthlySalary: request.MinimumMonthlySalary,
            isActive: request.IsActive,
            repaymentFrequency: request.RepaymentFrequency);

        // Audit Log
        var audit = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.LoanPlanUpdated,
            resourceType: AuditResourceTypes.LoanPlan,
            resourceId: plan.Id.ToString(),
            organizationId: organizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                plan.Name,
                plan.MinimumAmount,
                plan.MaximumAmount,
                plan.InterestRate,
                plan.IsActive
            }));
        _dbContext.AuditLogs.Add(audit);

        // Outbox Event
        _outboxService.Write(new LoanPlanUpdatedDomainEvent(plan));

        await _dbContext.SaveChangesAsync(cancellationToken);
        LogPlanUpdated(_logger, plan.Id, organizationId);

        return MapToDto(plan);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CorporateLoanPlanDto>> GetPlansForOrgAsync(
        Guid organizationId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CorporateLoanPlans
            .AsNoTracking()
            .Where(p => p.OrganizationId == organizationId);

        if (activeOnly)
        {
            query = query.Where(p => p.IsActive);
        }

        var plans = await query
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return plans.Select(MapToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<CorporateLoanPlanDto?> GetPlanByIdAsync(
        Guid organizationId,
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        var plan = await _dbContext.CorporateLoanPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == planId && p.OrganizationId == organizationId, cancellationToken);

        return plan != null ? MapToDto(plan) : null;
    }

    private static CorporateLoanPlanDto MapToDto(CorporateLoanPlan plan)
    {
        return new CorporateLoanPlanDto(
            Id: plan.Id,
            OrganizationId: plan.OrganizationId,
            Name: plan.Name,
            Description: plan.Description,
            MinimumAmount: plan.MinimumAmount,
            MaximumAmount: plan.MaximumAmount,
            InterestRate: plan.InterestRate,
            MinimumDurationMonths: plan.MinimumDurationMonths,
            MaximumDurationMonths: plan.MaximumDurationMonths,
            RepaymentFrequency: plan.RepaymentFrequency,
            MinimumMonthlySalary: plan.MinimumMonthlySalary,
            IsActive: plan.IsActive,
            CreatedAtUtc: plan.CreatedAtUtc,
            UpdatedAtUtc: plan.UpdatedAtUtc);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Created Corporate Loan Plan {PlanId} for Org {OrgId}")]
    private static partial void LogPlanCreated(ILogger logger, Guid planId, Guid orgId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Updated Corporate Loan Plan {PlanId} for Org {OrgId}")]
    private static partial void LogPlanUpdated(ILogger logger, Guid planId, Guid orgId);
}
