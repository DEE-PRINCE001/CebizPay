using CebizPay.Domain.Enums;

namespace CebizPay.Domain.Entities;

/// <summary>
/// Represents a job opening and recruitment posting published by an organization.
/// </summary>
public sealed class JobPosting
{
    /// <summary>Unique identifier of the job posting.</summary>
    public Guid Id { get; private set; }

    /// <summary>Organization owning this job posting.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Job title.</summary>
    public string Title { get; private set; } = null!;

    /// <summary>Detailed job description and overview.</summary>
    public string Description { get; private set; } = null!;

    /// <summary>Optional referenced department within the organization.</summary>
    public Guid? DepartmentId { get; private set; }

    /// <summary>Optional referenced workforce job role within the organization.</summary>
    public Guid? WorkforceRoleId { get; private set; }

    /// <summary>Optional referenced salary compensation structure.</summary>
    public Guid? SalaryLevelId { get; private set; }

    /// <summary>Employment arrangement type (FullTime, PartTime, Contract, etc.).</summary>
    public EmploymentType EmploymentType { get; private set; }

    /// <summary>Physical or remote workplace location.</summary>
    public string? Location { get; private set; }

    /// <summary>Candidate qualification requirements.</summary>
    public string? Requirements { get; private set; }

    /// <summary>Job duties and core responsibilities.</summary>
    public string? Responsibilities { get; private set; }

    /// <summary>Optional application submission deadline in UTC.</summary>
    public DateTime? ApplicationDeadline { get; private set; }

    /// <summary>Current lifecycle status of the job posting.</summary>
    public JobPostingStatus Status { get; private set; }

    /// <summary>Timestamp when the job posting was published to candidates in UTC.</summary>
    public DateTime? PublishedAtUtc { get; private set; }

    /// <summary>Timestamp when the job posting was closed or cancelled in UTC.</summary>
    public DateTime? ClosedAtUtc { get; private set; }

    /// <summary>User ID of the organization admin/recruiter who created this posting.</summary>
    public string CreatedByUserId { get; private set; } = null!;

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last update timestamp in UTC.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Collection of candidate applications submitted for this job opening.</summary>
    public ICollection<RecruitmentApplication> Applications { get; private set; } = new List<RecruitmentApplication>();

    /// <summary>Private constructor for EF Core.</summary>
    private JobPosting() { }

    /// <summary>
    /// Initializes a new draft JobPosting.
    /// </summary>
    public JobPosting(
        Guid organizationId,
        string title,
        string description,
        string createdByUserId,
        EmploymentType employmentType = EmploymentType.FullTime,
        Guid? departmentId = null,
        Guid? workforceRoleId = null,
        Guid? salaryLevelId = null,
        string? location = null,
        string? requirements = null,
        string? responsibilities = null,
        DateTime? applicationDeadline = null)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Job title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Job description is required.", nameof(description));
        if (string.IsNullOrWhiteSpace(createdByUserId))
            throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        Title = title.Trim();
        Description = description.Trim();
        CreatedByUserId = createdByUserId.Trim();
        EmploymentType = employmentType;
        DepartmentId = departmentId;
        WorkforceRoleId = workforceRoleId;
        SalaryLevelId = salaryLevelId;
        Location = location?.Trim();
        Requirements = requirements?.Trim();
        Responsibilities = responsibilities?.Trim();
        ApplicationDeadline = applicationDeadline;
        Status = JobPostingStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the job posting details. Allowed only when status is Draft or Published.
    /// </summary>
    public void Update(
        string title,
        string description,
        EmploymentType employmentType,
        Guid? departmentId,
        Guid? workforceRoleId,
        Guid? salaryLevelId,
        string? location,
        string? requirements,
        string? responsibilities,
        DateTime? applicationDeadline)
    {
        if (Status is JobPostingStatus.Closed or JobPostingStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot update job posting when in {Status} status.");
        }

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Job title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Job description is required.", nameof(description));

        Title = title.Trim();
        Description = description.Trim();
        EmploymentType = employmentType;
        DepartmentId = departmentId;
        WorkforceRoleId = workforceRoleId;
        SalaryLevelId = salaryLevelId;
        Location = location?.Trim();
        Requirements = requirements?.Trim();
        Responsibilities = responsibilities?.Trim();
        ApplicationDeadline = applicationDeadline;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Publishes a draft job posting to start accepting applications.
    /// </summary>
    public void Publish(DateTime utcNow)
    {
        if (Status != JobPostingStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot publish job posting from status {Status}. Only Draft postings can be published.");
        }

        if (ApplicationDeadline.HasValue && ApplicationDeadline.Value <= utcNow)
        {
            throw new InvalidOperationException("Cannot publish a job posting with an application deadline in the past.");
        }

        Status = JobPostingStatus.Published;
        PublishedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Closes an active job posting, terminating new candidate application intake.
    /// </summary>
    public void Close(DateTime utcNow)
    {
        if (Status != JobPostingStatus.Published)
        {
            throw new InvalidOperationException($"Cannot close job posting from status {Status}. Only Published postings can be closed.");
        }

        Status = JobPostingStatus.Closed;
        ClosedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Cancels a draft or published job posting.
    /// </summary>
    public void Cancel(DateTime utcNow)
    {
        if (Status is JobPostingStatus.Closed or JobPostingStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot cancel job posting in {Status} status.");
        }

        Status = JobPostingStatus.Cancelled;
        ClosedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Checks whether the job posting is active and currently eligible to receive candidate applications.
    /// </summary>
    public bool IsAcceptingApplications(DateTime utcNow)
    {
        if (Status != JobPostingStatus.Published)
        {
            return false;
        }

        if (ApplicationDeadline.HasValue && ApplicationDeadline.Value < utcNow)
        {
            return false;
        }

        return true;
    }
}
