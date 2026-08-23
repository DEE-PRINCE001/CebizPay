namespace CebizPay.Domain.Events;

/// <summary>
/// Domain event published when a department is created.
/// </summary>
public sealed record DepartmentCreatedDomainEvent(
    Guid DepartmentId,
    Guid OrganizationId,
    string Name,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a department is updated.
/// </summary>
public sealed record DepartmentUpdatedDomainEvent(
    Guid DepartmentId,
    Guid OrganizationId,
    string Name,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a department is deleted.
/// </summary>
public sealed record DepartmentDeletedDomainEvent(
    Guid DepartmentId,
    Guid OrganizationId,
    string Name,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a workforce role is created.
/// </summary>
public sealed record WorkforceRoleCreatedDomainEvent(
    Guid RoleId,
    Guid OrganizationId,
    string Title,
    Guid? DepartmentId,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a workforce role is updated.
/// </summary>
public sealed record WorkforceRoleUpdatedDomainEvent(
    Guid RoleId,
    Guid OrganizationId,
    string Title,
    Guid? DepartmentId,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a workforce role is deleted.
/// </summary>
public sealed record WorkforceRoleDeletedDomainEvent(
    Guid RoleId,
    Guid OrganizationId,
    string Title,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a salary level is created.
/// </summary>
public sealed record SalaryLevelCreatedDomainEvent(
    Guid SalaryLevelId,
    Guid OrganizationId,
    string LevelName,
    decimal BaseAmount,
    string Currency,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a salary level is updated.
/// </summary>
public sealed record SalaryLevelUpdatedDomainEvent(
    Guid SalaryLevelId,
    Guid OrganizationId,
    string LevelName,
    decimal BaseAmount,
    string Currency,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a salary level is deleted.
/// </summary>
public sealed record SalaryLevelDeletedDomainEvent(
    Guid SalaryLevelId,
    Guid OrganizationId,
    string LevelName,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when staff workforce details (department, role, salary level) are assigned or updated.
/// </summary>
public sealed record StaffAssignedDomainEvent(
    Guid MembershipId,
    Guid OrganizationId,
    string UserId,
    Guid? DepartmentId,
    Guid? WorkforceRoleId,
    Guid? SalaryLevelId,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a staff member is directly created/onboarded without an invitation.
/// </summary>
public sealed record StaffDirectCreatedDomainEvent(
    Guid MembershipId,
    Guid OrganizationId,
    string UserId,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a suspended or terminated staff member is reactivated.
/// </summary>
public sealed record StaffMembershipReactivatedDomainEvent(
    Guid MembershipId,
    Guid OrganizationId,
    string UserId,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when a staff member is terminated/offboarded.
/// </summary>
public sealed record StaffMembershipTerminatedDomainEvent(
    Guid MembershipId,
    Guid OrganizationId,
    string UserId,
    string Reason,
    DateTime OccurredOnUtc);

/// <summary>
/// Domain event published when bulk staff invitations are created.
/// </summary>
public sealed record StaffBulkInvitationsCreatedDomainEvent(
    Guid OrganizationId,
    int TotalInvitations,
    DateTime OccurredOnUtc);
