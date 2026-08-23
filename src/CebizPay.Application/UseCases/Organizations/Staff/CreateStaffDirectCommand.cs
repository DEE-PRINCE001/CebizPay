using System.Security.Cryptography;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Events;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Staff;

/// <summary>
/// Command for direct onboarding of a staff member without an external invitation.
/// </summary>
public sealed record CreateStaffDirectCommand(
    Guid OrganizationId,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber = null,
    Guid? DepartmentId = null,
    Guid? WorkforceRoleId = null,
    Guid? SalaryLevelId = null,
    MembershipRoleType Role = MembershipRoleType.Member) : IRequest<Guid>;

/// <summary>
/// Validator for CreateStaffDirectCommand.
/// </summary>
public sealed class CreateStaffDirectCommandValidator : AbstractValidator<CreateStaffDirectCommand>
{
    /// <summary>
    /// Initializes validation rules for CreateStaffDirectCommand.
    /// </summary>
    public CreateStaffDirectCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("OrganizationId is required.");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("A valid email is required.");
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("FirstName is required.").MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().WithMessage("LastName is required.").MaximumLength(100);
    }
}

/// <summary>
/// Handler for CreateStaffDirectCommand.
/// </summary>
public sealed class CreateStaffDirectCommandHandler : IRequestHandler<CreateStaffDirectCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;
    private readonly IOutboxService _outboxService;

    /// <summary>
    /// Initializes a new instance of <see cref="CreateStaffDirectCommandHandler"/>.
    /// </summary>
    public CreateStaffDirectCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext,
        ICurrentUserService currentUserService,
        IIdentityService identityService,
        IOutboxService outboxService)
    {
        _dbContext = dbContext;
        _orgContext = orgContext;
        _currentUserService = currentUserService;
        _identityService = identityService;
        _outboxService = outboxService;
    }

    /// <inheritdoc/>
    public async Task<Guid> Handle(CreateStaffDirectCommand request, CancellationToken cancellationToken)
    {
        var hasAccess = await _orgContext.HasAccessToOrganizationAsync(request.OrganizationId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"Tenant isolation check failed for organization {request.OrganizationId}.");
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization {request.OrganizationId} not found.");

        if (!org.CanConfigureHris())
        {
            throw new InvalidOperationException("Cannot configure HRIS structure while organization status is suspended.");
        }

        if (request.DepartmentId.HasValue)
        {
            var deptExists = await _dbContext.Departments.AnyAsync(
                d => d.Id == request.DepartmentId.Value && d.OrganizationId == request.OrganizationId,
                cancellationToken);
            if (!deptExists)
            {
                throw new KeyNotFoundException($"Department {request.DepartmentId.Value} not found in organization {request.OrganizationId}.");
            }
        }

        if (request.WorkforceRoleId.HasValue)
        {
            var roleExists = await _dbContext.WorkforceRoles.AnyAsync(
                r => r.Id == request.WorkforceRoleId.Value && r.OrganizationId == request.OrganizationId,
                cancellationToken);
            if (!roleExists)
            {
                throw new KeyNotFoundException($"Workforce role {request.WorkforceRoleId.Value} not found in organization {request.OrganizationId}.");
            }
        }

        if (request.SalaryLevelId.HasValue)
        {
            var levelExists = await _dbContext.SalaryLevels.AnyAsync(
                s => s.Id == request.SalaryLevelId.Value && s.OrganizationId == request.OrganizationId,
                cancellationToken);
            if (!levelExists)
            {
                throw new KeyNotFoundException($"Salary level {request.SalaryLevelId.Value} not found in organization {request.OrganizationId}.");
            }
        }

        var trimmedEmail = request.Email.Trim().ToLowerInvariant();
        var (foundUser, userId, _, _) = await _identityService.FindUserByEmailAsync(trimmedEmail, cancellationToken);

        if (!foundUser)
        {
            // Generate a secure temporary placeholder password
            var tempPassword = $"Tmp@{RandomNumberGenerator.GetInt32(100000, 999999)}!{Guid.NewGuid().ToString("N")[..4]}";
            var regResult = await _identityService.RegisterUserAsync(trimmedEmail, tempPassword, request.PhoneNumber, cancellationToken);
            if (!regResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create user account: {string.Join(", ", regResult.Errors)}");
            }
            userId = regResult.UserId;

            var newProfile = new IndividualProfile(userId, request.FirstName.Trim(), request.LastName.Trim());
            newProfile.UpdateProfessionalStatus(ProfessionalStatus.Staff);
            _dbContext.IndividualProfiles.Add(newProfile);
        }
        else
        {
            var profile = await _dbContext.IndividualProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
            if (profile == null)
            {
                profile = new IndividualProfile(userId, request.FirstName.Trim(), request.LastName.Trim());
                profile.UpdateProfessionalStatus(ProfessionalStatus.Staff);
                _dbContext.IndividualProfiles.Add(profile);
            }
            else
            {
                profile.UpdateProfessionalStatus(ProfessionalStatus.Staff);
            }
        }

        var existingMembership = await _dbContext.OrganizationMemberships.FirstOrDefaultAsync(
            m => m.UserId == userId && m.OrganizationId == request.OrganizationId,
            cancellationToken);

        OrganizationMembership membership;
        if (existingMembership != null)
        {
            if (existingMembership.Status == MembershipStatus.Active)
            {
                throw new InvalidOperationException($"User '{trimmedEmail}' is already an active staff member in this organization.");
            }

            existingMembership.ReactivateWorkAccess();
            existingMembership.ChangeRole(request.Role);
            existingMembership.AssignWorkforceDetails(request.DepartmentId, request.WorkforceRoleId, request.SalaryLevelId);
            membership = existingMembership;
        }
        else
        {
            membership = new OrganizationMembership(
                userId,
                request.OrganizationId,
                request.Role,
                request.DepartmentId,
                request.WorkforceRoleId,
                request.SalaryLevelId);
            _dbContext.OrganizationMemberships.Add(membership);
        }

        var actorUserId = _currentUserService.UserId ?? "SYSTEM";
        var auditLog = AuditLog.Create(
            actorId: actorUserId,
            action: AuditActions.StaffCreated,
            resourceType: AuditResourceTypes.StaffMember,
            resourceId: membership.Id.ToString(),
            organizationId: request.OrganizationId,
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                membership.Id,
                membership.UserId,
                Email = trimmedEmail,
                request.FirstName,
                request.LastName,
                request.DepartmentId,
                request.WorkforceRoleId,
                request.SalaryLevelId
            }));
        _dbContext.AuditLogs.Add(auditLog);

        _outboxService.Write(new StaffDirectCreatedDomainEvent(
            membership.Id,
            request.OrganizationId,
            userId,
            DateTime.UtcNow));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return membership.Id;
    }
}
