namespace CebizPay.Application.UseCases.Organizations.Workforce;

/// <summary>
/// DTO representing an organization department.
/// </summary>
public sealed record DepartmentDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    DateTime CreatedAtUtc,
    int ActiveStaffCount = 0);

/// <summary>
/// DTO representing a workforce job role within an organization.
/// </summary>
public sealed record WorkforceRoleDto(
    Guid Id,
    Guid OrganizationId,
    Guid? DepartmentId,
    string? DepartmentName,
    string Title,
    string? Description,
    DateTime CreatedAtUtc,
    int ActiveStaffCount = 0);

/// <summary>
/// DTO representing an organization salary level structure.
/// </summary>
public sealed record SalaryLevelDto(
    Guid Id,
    Guid OrganizationId,
    string LevelName,
    decimal BaseAmount,
    string Currency,
    DateTime CreatedAtUtc,
    int ActiveStaffCount = 0);
