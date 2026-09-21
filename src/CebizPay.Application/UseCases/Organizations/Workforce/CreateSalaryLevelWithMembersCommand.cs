using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Workforce;

/// <summary>
/// Response DTO returning a salary level and the count of assigned staff members.
/// </summary>
public sealed record SalaryLevelWithMembersDto(
    Guid Id,
    Guid OrganizationId,
    string LevelName,
    decimal BaseAmount,
    string Currency,
    int AssignedStaffCount,
    DateTime CreatedAtUtc);

/// <summary>
/// Command to atomically create an organization salary level tier and assign initial staff members.
/// </summary>
public sealed record CreateSalaryLevelWithMembersCommand(
    Guid OrganizationId,
    string LevelName,
    decimal BaseAmount,
    string Currency = "NGN",
    IReadOnlyList<Guid>? StaffMembershipIds = null) : IRequest<SalaryLevelWithMembersDto>;

/// <summary>
/// Validator for CreateSalaryLevelWithMembersCommand.
/// </summary>
public sealed class CreateSalaryLevelWithMembersCommandValidator : AbstractValidator<CreateSalaryLevelWithMembersCommand>
{
    private static readonly string[] AllowedCurrencies = ["NGN", "INT-NGN", "USDT", "USD", "GHS", "EUR", "INR"];

    /// <summary>
    /// Initializes validation rules for CreateSalaryLevelWithMembersCommand.
    /// </summary>
    public CreateSalaryLevelWithMembersCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty()
            .WithMessage("OrganizationId is required.");

        RuleFor(x => x.LevelName)
            .NotEmpty()
            .WithMessage("LevelName is required.")
            .MaximumLength(100);

        RuleFor(x => x.BaseAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("BaseAmount cannot be negative.");

        RuleFor(x => x.Currency)
            .Must(c => AllowedCurrencies.Contains(c.ToUpperInvariant()))
            .WithMessage("Currency must be a supported V1 currency.");

        When(x => x.StaffMembershipIds != null, () =>
        {
            RuleForEach(x => x.StaffMembershipIds)
                .NotEmpty()
                .WithMessage("Staff membership ID cannot be empty.");
        });
    }
}

/// <summary>
/// Handler for CreateSalaryLevelWithMembersCommand.
/// </summary>
public sealed class CreateSalaryLevelWithMembersCommandHandler : IRequestHandler<CreateSalaryLevelWithMembersCommand, SalaryLevelWithMembersDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="CreateSalaryLevelWithMembersCommandHandler"/>.
    /// </summary>
    public CreateSalaryLevelWithMembersCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService,
        IOutboxService outboxService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _outboxService = outboxService ?? throw new ArgumentNullException(nameof(outboxService));
    }

    /// <inheritdoc/>
    public async Task<SalaryLevelWithMembersDto> Handle(CreateSalaryLevelWithMembersCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken).ConfigureAwait(false);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Organization {request.OrganizationId} not found.");

        if (!org.CanConfigureHris())
        {
            throw new InvalidOperationException("Cannot configure HRIS structure while organization status is suspended.");
        }

        var trimmedName = request.LevelName.Trim();
        var lowerName = trimmedName.ToLowerInvariant();

#pragma warning disable CA1862, CA1304, CA1311
        var exists = await _dbContext.SalaryLevels.AnyAsync(
            s => s.OrganizationId == request.OrganizationId && s.LevelName.ToLower() == lowerName,
            cancellationToken).ConfigureAwait(false);
#pragma warning restore CA1862, CA1304, CA1311

        if (exists)
        {
            throw new InvalidOperationException($"Salary level with name '{trimmedName}' already exists in this organization.");
        }

        var salaryLevel = new SalaryLevel(request.OrganizationId, trimmedName, request.BaseAmount, request.Currency);
        _dbContext.SalaryLevels.Add(salaryLevel);

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var now = DateTime.UtcNow;

        var levelAuditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.SalaryLevelCreated,
            resourceType: AuditResourceTypes.SalaryLevel,
            resourceId: salaryLevel.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new { salaryLevel.Id, salaryLevel.LevelName, salaryLevel.BaseAmount, salaryLevel.Currency }));
        _dbContext.AuditLogs.Add(levelAuditLog);

        _outboxService.Write(new SalaryLevelCreatedDomainEvent(
            SalaryLevelId: salaryLevel.Id,
            OrganizationId: request.OrganizationId,
            LevelName: salaryLevel.LevelName,
            BaseAmount: salaryLevel.BaseAmount,
            Currency: salaryLevel.Currency,
            OccurredOnUtc: now));

        var assignedCount = 0;

        if (request.StaffMembershipIds != null && request.StaffMembershipIds.Count > 0)
        {
            var distinctIds = request.StaffMembershipIds.Distinct().ToList();

            var memberships = await _dbContext.OrganizationMemberships
                .Where(m => m.OrganizationId == request.OrganizationId && distinctIds.Contains(m.Id))
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            if (memberships.Count != distinctIds.Count)
            {
                var foundIds = memberships.Select(m => m.Id).ToHashSet();
                var missingIds = distinctIds.Where(id => !foundIds.Contains(id)).ToList();
                throw new KeyNotFoundException($"Staff membership(s) [{string.Join(", ", missingIds)}] not found in organization {request.OrganizationId}.");
            }

            var terminated = memberships.FirstOrDefault(m => m.Status == MembershipStatus.Terminated);
            if (terminated != null)
            {
                throw new InvalidOperationException($"Cannot assign salary level to terminated staff member '{terminated.Id}'.");
            }

            foreach (var membership in memberships)
            {
                var beforeJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    membership.Id,
                    membership.DepartmentId,
                    membership.WorkforceRoleId,
                    membership.SalaryLevelId
                });

                membership.AssignWorkforceDetails(membership.DepartmentId, membership.WorkforceRoleId, salaryLevel.Id);

                var afterJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    membership.Id,
                    membership.DepartmentId,
                    membership.WorkforceRoleId,
                    membership.SalaryLevelId
                });

                var staffAudit = AuditLog.Create(
                    actorId: actorUserId,
                    action: AuditActions.StaffAssigned,
                    resourceType: AuditResourceTypes.StaffMember,
                    resourceId: membership.Id.ToString(),
                    organizationId: request.OrganizationId,
                    beforeJson: beforeJson,
                    afterJson: afterJson);
                _dbContext.AuditLogs.Add(staffAudit);

                _outboxService.Write(new StaffAssignedDomainEvent(
                    MembershipId: membership.Id,
                    OrganizationId: request.OrganizationId,
                    UserId: membership.UserId,
                    DepartmentId: membership.DepartmentId,
                    WorkforceRoleId: membership.WorkforceRoleId,
                    SalaryLevelId: salaryLevel.Id,
                    OccurredOnUtc: now));
            }

            assignedCount = memberships.Count;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SalaryLevelWithMembersDto(
            Id: salaryLevel.Id,
            OrganizationId: salaryLevel.OrganizationId,
            LevelName: salaryLevel.LevelName,
            BaseAmount: salaryLevel.BaseAmount,
            Currency: salaryLevel.Currency,
            AssignedStaffCount: assignedCount,
            CreatedAtUtc: salaryLevel.CreatedAtUtc);
    }
}
