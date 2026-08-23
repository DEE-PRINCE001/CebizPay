using CebizPay.Domain.Enums;

namespace CebizPay.Application.UseCases.Organizations.Staff;

/// <summary>
/// DTO representing a summarized staff entry in the staff directory.
/// </summary>
public sealed record StaffSummaryDto(
    Guid MembershipId,
    string UserId,
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber,
    string? KycStatus,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? WorkforceRoleId,
    string? RoleTitle,
    Guid? SalaryLevelId,
    string? SalaryLevelName,
    decimal? BaseSalary,
    string? Currency,
    string Role,
    string Status,
    DateTime JoinedAtUtc,
    DateTime? SuspendedAtUtc,
    string? SuspensionReason);

/// <summary>
/// DTO representing a detailed staff profile within an organization.
/// </summary>
public sealed record StaffProfileDto(
    Guid MembershipId,
    string UserId,
    Guid OrganizationId,
    string? FirstName,
    string? LastName,
    string? MiddleName,
    string? Email,
    string? PhoneNumber,
    string? KycStatus,
    string? ProfessionalStatus,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? WorkforceRoleId,
    string? RoleTitle,
    Guid? SalaryLevelId,
    string? SalaryLevelName,
    decimal? BaseSalary,
    string? Currency,
    string Role,
    string Status,
    DateTime JoinedAtUtc,
    DateTime? SuspendedAtUtc,
    string? SuspensionReason);

/// <summary>
/// Individual result for a single recipient within a bulk invitation operation.
/// </summary>
public sealed record BulkInviteItemResultDto(
    string Email,
    bool Success,
    Guid? InvitationId,
    string? InvitationCode,
    string? Error);

/// <summary>
/// Summary response envelope for a bulk staff invitation request.
/// </summary>
public sealed record BulkInviteSummaryDto(
    int TotalRequested,
    int TotalSuccess,
    int TotalFailed,
    IReadOnlyList<BulkInviteItemResultDto> Results);
