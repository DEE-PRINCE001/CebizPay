using CebizPay.Domain.Enums;

namespace CebizPay.Application.UseCases.Organizations.Recruitment;

/// <summary>
/// DTO representing an organization job posting with management details.
/// </summary>
public sealed record JobPostingDto(
    Guid Id,
    Guid OrganizationId,
    string Title,
    string Description,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? WorkforceRoleId,
    string? WorkforceRoleTitle,
    Guid? SalaryLevelId,
    string? SalaryLevelName,
    decimal? BaseSalary,
    string? SalaryCurrency,
    EmploymentType EmploymentType,
    string? Location,
    string? Requirements,
    string? Responsibilities,
    DateTime? ApplicationDeadline,
    JobPostingStatus Status,
    DateTime? PublishedAtUtc,
    DateTime? ClosedAtUtc,
    string CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int ApplicationCount = 0);

/// <summary>
/// Safe DTO representing a published job posting for public candidates.
/// </summary>
public sealed record PublicJobPostingDto(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    string Title,
    string Description,
    string? DepartmentName,
    string? WorkforceRoleTitle,
    EmploymentType EmploymentType,
    string? Location,
    string? Requirements,
    string? Responsibilities,
    DateTime? ApplicationDeadline,
    DateTime? PublishedAtUtc);

/// <summary>
/// DTO representing a candidate employment application.
/// </summary>
public sealed record RecruitmentApplicationDto(
    Guid Id,
    Guid JobPostingId,
    string JobTitle,
    Guid OrganizationId,
    string? ApplicantUserId,
    string ApplicantName,
    string ApplicantEmail,
    string ApplicantPhone,
    string? ResumeReference,
    string? CoverLetter,
    ApplicationStatus Status,
    DateTime AppliedAtUtc,
    DateTime? ReviewedAtUtc,
    string? ReviewedByUserId,
    string? RejectionReason,
    string? ReviewNotes);

/// <summary>
/// API request payload for creating a draft job posting.
/// </summary>
public sealed record CreateJobPostingApiRequest(
    string Title,
    string Description,
    EmploymentType EmploymentType = EmploymentType.FullTime,
    Guid? DepartmentId = null,
    Guid? WorkforceRoleId = null,
    Guid? SalaryLevelId = null,
    string? Location = null,
    string? Requirements = null,
    string? Responsibilities = null,
    DateTime? ApplicationDeadline = null);

/// <summary>
/// API request payload for updating a job posting.
/// </summary>
public sealed record UpdateJobPostingApiRequest(
    string Title,
    string Description,
    EmploymentType EmploymentType,
    Guid? DepartmentId = null,
    Guid? WorkforceRoleId = null,
    Guid? SalaryLevelId = null,
    string? Location = null,
    string? Requirements = null,
    string? Responsibilities = null,
    DateTime? ApplicationDeadline = null);

/// <summary>
/// API request payload for candidate job application submission.
/// </summary>
public sealed record SubmitApplicationApiRequest(
    string ApplicantName,
    string ApplicantEmail,
    string ApplicantPhone,
    string? ResumeReference = null,
    string? CoverLetter = null);

/// <summary>
/// API request payload for moving an application to under review.
/// </summary>
public sealed record ReviewApplicationApiRequest(string? Notes = null);

/// <summary>
/// API request payload for shortlisting a candidate application.
/// </summary>
public sealed record ShortlistApplicationApiRequest(string? Notes = null);

/// <summary>
/// API request payload for rejecting a candidate application.
/// </summary>
public sealed record RejectApplicationApiRequest(string RejectionReason, string? Notes = null);

/// <summary>
/// API request payload for accepting a candidate application.
/// </summary>
public sealed record AcceptApplicationApiRequest(string? Notes = null);
