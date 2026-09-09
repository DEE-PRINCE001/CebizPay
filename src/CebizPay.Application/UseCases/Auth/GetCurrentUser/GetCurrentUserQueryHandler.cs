using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Permissions;
using MediatR;

namespace CebizPay.Application.UseCases.Auth.GetCurrentUser;

/// <summary>
/// Handler for executing GetCurrentUserQuery.
/// Assembles user identity, personal profile, admin privileges, and organization memberships.
/// </summary>
public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, CurrentUserDto>
{
    private static readonly string[] StandardMemberPermissions =
    [
        Permissions.PayrollView,
        Permissions.WalletView,
        Permissions.LoanView,
        Permissions.LoanRepaymentView,
        Permissions.LoanCreate,
        Permissions.SavingsView,
        Permissions.SavingsCreate,
        Permissions.SavingsContribute,
        Permissions.SavingsWithdraw,
        Permissions.ThriftView,
        Permissions.ThriftCreate,
        Permissions.ThriftInvite,
        Permissions.ThriftContribute,
        Permissions.ThriftPayoutView,
        Permissions.StaffView
    ];

    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentOrganizationContext _orgContext;

    /// <summary>
    /// Initializes a new instance of <see cref="GetCurrentUserQueryHandler"/>.
    /// </summary>
    public GetCurrentUserQueryHandler(
        ICurrentUserService currentUserService,
        IIdentityService identityService,
        IApplicationDbContext dbContext,
        ICurrentOrganizationContext orgContext)
    {
        _currentUserService = currentUserService;
        _identityService = identityService;
        _dbContext = dbContext;
        _orgContext = orgContext;
    }

    /// <inheritdoc/>
    public async Task<CurrentUserDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var identity = await _identityService.GetUserIdentityByIdAsync(userId, cancellationToken);
        if (identity == null)
        {
            throw new KeyNotFoundException($"User with ID '{userId}' was not found.");
        }

        // 1. Individual Profile
        var profile = await _dbContext.IndividualProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        var firstName = profile?.FirstName ?? string.Empty;
        var lastName = profile?.LastName ?? string.Empty;
        var middleName = profile?.MiddleName;
        var fullName = profile != null
            ? (string.IsNullOrWhiteSpace(middleName) ? $"{firstName} {lastName}".Trim() : $"{firstName} {middleName} {lastName}".Trim())
            : (identity.Email.Contains('@') ? identity.Email.Split('@')[0] : "User");

        var kycStatus = profile?.KycStatus.ToString() ?? KycStatus.Pending.ToString();
        var profStatus = profile?.ProfessionalStatus.ToString() ?? ProfessionalStatus.NotAStaff.ToString();
        var isSubjectToCap = profile?.IsSubjectToTransactionCap() ?? true;
        var canAcceptStaff = profile?.CanAcceptStaffInvitation() ?? false;

        // 2. Platform Admin Profile
        var admin = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == userId && a.IsActive && !a.IsDeleted, cancellationToken);

        UserAdminDto? adminDto = null;
        if (admin != null)
        {
            IReadOnlyList<string> adminPermissions;
            if (admin.Role == AdminRoleType.SuperAdmin)
            {
                adminPermissions = Permissions.OrgSuperAdminPermissions
                    .Union(Permissions.ReadOnlyAdminPermissions)
                    .OrderBy(p => p)
                    .ToList();
            }
            else if (admin.Role == AdminRoleType.Auditor)
            {
                adminPermissions = Permissions.ReadOnlyAdminPermissions.OrderBy(p => p).ToList();
            }
            else
            {
                adminPermissions = admin.PermissionsList.OrderBy(p => p).ToList();
            }

            adminDto = new UserAdminDto(
                Role: admin.Role.ToString(),
                IsActive: admin.IsActive,
                IsMfaEnabled: admin.IsMfaEnabled,
                Permissions: adminPermissions);
        }

        // 3. Organization Memberships
        var memberships = await _dbContext.OrganizationMemberships
            .Where(m => m.UserId == userId)
            .ToListAsync(cancellationToken);

        var orgDtos = new List<UserOrganizationMembershipDto>();
        if (memberships.Count > 0)
        {
            var orgIds = memberships.Select(m => m.OrganizationId).Distinct().ToList();
            var orgsList = await _dbContext.Organizations
                .Where(o => orgIds.Contains(o.Id) && !o.IsDeleted)
                .ToListAsync(cancellationToken);
            var orgs = orgsList.ToDictionary(o => o.Id);

            var deptIds = memberships.Where(m => m.DepartmentId.HasValue).Select(m => m.DepartmentId!.Value).Distinct().ToList();
            var deptsList = deptIds.Count > 0
                ? await _dbContext.Departments.Where(d => deptIds.Contains(d.Id)).ToListAsync(cancellationToken)
                : [];
            var depts = deptsList.ToDictionary(d => d.Id);

            var roleIds = memberships.Where(m => m.WorkforceRoleId.HasValue).Select(m => m.WorkforceRoleId!.Value).Distinct().ToList();
            var rolesList = roleIds.Count > 0
                ? await _dbContext.WorkforceRoles.Where(r => roleIds.Contains(r.Id)).ToListAsync(cancellationToken)
                : [];
            var roles = rolesList.ToDictionary(r => r.Id);

            var levelIds = memberships.Where(m => m.SalaryLevelId.HasValue).Select(m => m.SalaryLevelId!.Value).Distinct().ToList();
            var levelsList = levelIds.Count > 0
                ? await _dbContext.SalaryLevels.Where(l => levelIds.Contains(l.Id)).ToListAsync(cancellationToken)
                : [];
            var levels = levelsList.ToDictionary(l => l.Id);

            foreach (var m in memberships)
            {
                if (!orgs.TryGetValue(m.OrganizationId, out var org))
                {
                    continue;
                }

                var deptName = m.DepartmentId.HasValue && depts.TryGetValue(m.DepartmentId.Value, out var d) ? d.Name : null;
                var roleTitle = m.WorkforceRoleId.HasValue && roles.TryGetValue(m.WorkforceRoleId.Value, out var r) ? r.Title : null;
                var levelName = m.SalaryLevelId.HasValue && levels.TryGetValue(m.SalaryLevelId.Value, out var l) ? l.LevelName : null;

                IReadOnlyList<string> perms = [];
                if (m.Status == MembershipStatus.Active)
                {
                    if (m.Role == MembershipRoleType.Owner || m.Role == MembershipRoleType.Admin)
                    {
                        perms = Permissions.OrgSuperAdminPermissions.Union(Permissions.ReadOnlyAdminPermissions).OrderBy(p => p).ToList();
                    }
                    else if (m.Role == MembershipRoleType.PayrollManager)
                    {
                        perms = Permissions.FinanceManagerPermissions.Union(Permissions.ReadOnlyAdminPermissions).OrderBy(p => p).ToList();
                    }
                    else if (m.Role == MembershipRoleType.HrManager)
                    {
                        perms = Permissions.HrManagerPermissions.Union(Permissions.ReadOnlyAdminPermissions).OrderBy(p => p).ToList();
                    }
                    else
                    {
                        perms = StandardMemberPermissions.OrderBy(p => p).ToList();
                    }
                }

                orgDtos.Add(new UserOrganizationMembershipDto(
                    OrganizationId: m.OrganizationId,
                    CompanyName: org.CompanyName,
                    Role: m.Role.ToString(),
                    Status: m.Status.ToString(),
                    IsActive: m.Status == MembershipStatus.Active,
                    DepartmentId: m.DepartmentId,
                    DepartmentName: deptName,
                    WorkforceRoleId: m.WorkforceRoleId,
                    WorkforceRoleTitle: roleTitle,
                    SalaryLevelId: m.SalaryLevelId,
                    SalaryLevelName: levelName,
                    JoinedAtUtc: m.JoinedAtUtc,
                    Permissions: perms));
            }
        }

        var activeOrgId = _orgContext.CurrentOrganizationId;

        return new CurrentUserDto(
            UserId: identity.UserId,
            Email: identity.Email,
            EmailConfirmed: identity.EmailConfirmed,
            PhoneNumber: identity.PhoneNumber,
            PhoneNumberConfirmed: identity.PhoneNumberConfirmed,
            TwoFactorEnabled: identity.TwoFactorEnabled,
            HasTransactionPin: identity.HasTransactionPin,
            FirstName: firstName,
            LastName: lastName,
            MiddleName: middleName,
            FullName: fullName,
            KycStatus: kycStatus,
            ProfessionalStatus: profStatus,
            IsSubjectToTransactionCap: isSubjectToCap,
            CanAcceptStaffInvitation: canAcceptStaff,
            CreatedAtUtc: identity.CreatedAtUtc,
            AdminProfile: adminDto,
            Organizations: orgDtos,
            ActiveOrganizationId: activeOrgId);
    }
}
