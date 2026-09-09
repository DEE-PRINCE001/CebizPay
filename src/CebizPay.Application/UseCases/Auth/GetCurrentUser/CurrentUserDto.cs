namespace CebizPay.Application.UseCases.Auth.GetCurrentUser;

/// <summary>
/// Authoritative profile and identity details of the authenticated user for frontend context.
/// </summary>
public sealed record CurrentUserDto(
    string UserId,
    string Email,
    bool EmailConfirmed,
    string? PhoneNumber,
    bool PhoneNumberConfirmed,
    bool TwoFactorEnabled,
    bool HasTransactionPin,
    string FirstName,
    string LastName,
    string? MiddleName,
    string FullName,
    string KycStatus,
    string ProfessionalStatus,
    bool IsSubjectToTransactionCap,
    bool CanAcceptStaffInvitation,
    DateTime CreatedAtUtc,
    UserAdminDto? AdminProfile,
    IReadOnlyList<UserOrganizationMembershipDto> Organizations,
    Guid? ActiveOrganizationId);

/// <summary>
/// Administrative profile details of the user if assigned a platform admin role.
/// </summary>
public sealed record UserAdminDto(
    string Role,
    bool IsActive,
    bool IsMfaEnabled,
    IReadOnlyList<string> Permissions);

/// <summary>
/// Organization workplace membership details for the user.
/// </summary>
public sealed record UserOrganizationMembershipDto(
    Guid OrganizationId,
    string CompanyName,
    string Role,
    string Status,
    bool IsActive,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? WorkforceRoleId,
    string? WorkforceRoleTitle,
    Guid? SalaryLevelId,
    string? SalaryLevelName,
    DateTime JoinedAtUtc,
    IReadOnlyList<string> Permissions);
