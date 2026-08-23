using CebizPay.Domain.Enums;

namespace CebizPay.Domain.Entities;

/// <summary>
/// Represents an employment application submitted by a candidate for a specific job posting.
/// </summary>
public sealed class RecruitmentApplication
{
    /// <summary>Unique identifier of the application.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identifier of the target job posting.</summary>
    public Guid JobPostingId { get; private set; }

    /// <summary>Organization hosting the recruitment opening.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Optional user ID if the applicant is an authenticated platform user.</summary>
    public string? ApplicantUserId { get; private set; }

    /// <summary>Applicant full name.</summary>
    public string ApplicantName { get; private set; } = null!;

    /// <summary>Applicant contact email address.</summary>
    public string ApplicantEmail { get; private set; } = null!;

    /// <summary>Applicant phone number.</summary>
    public string ApplicantPhone { get; private set; } = null!;

    /// <summary>Optional resume/CV file URL or reference identifier.</summary>
    public string? ResumeReference { get; private set; }

    /// <summary>Optional cover letter or personal statement.</summary>
    public string? CoverLetter { get; private set; }

    /// <summary>Current review lifecycle status.</summary>
    public ApplicationStatus Status { get; private set; }

    /// <summary>Timestamp when the application was submitted in UTC.</summary>
    public DateTime AppliedAtUtc { get; private set; }

    /// <summary>Timestamp when the application status was last reviewed/updated in UTC.</summary>
    public DateTime? ReviewedAtUtc { get; private set; }

    /// <summary>User ID of the recruiter/HR reviewer who processed this status.</summary>
    public string? ReviewedByUserId { get; private set; }

    /// <summary>Reason for rejection if application was declined.</summary>
    public string? RejectionReason { get; private set; }

    /// <summary>Internal recruitment evaluation notes.</summary>
    public string? ReviewNotes { get; private set; }

    /// <summary>Creation timestamp in UTC.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Last update timestamp in UTC.</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Private constructor for EF Core.</summary>
    private RecruitmentApplication() { }

    /// <summary>
    /// Initializes a new candidate job application in Submitted status.
    /// </summary>
    public RecruitmentApplication(
        Guid jobPostingId,
        Guid organizationId,
        string applicantName,
        string applicantEmail,
        string applicantPhone,
        string? applicantUserId = null,
        string? resumeReference = null,
        string? coverLetter = null)
    {
        if (jobPostingId == Guid.Empty)
            throw new ArgumentException("JobPostingId is required.", nameof(jobPostingId));
        if (organizationId == Guid.Empty)
            throw new ArgumentException("OrganizationId is required.", nameof(organizationId));
        if (string.IsNullOrWhiteSpace(applicantName))
            throw new ArgumentException("Applicant name is required.", nameof(applicantName));
        if (string.IsNullOrWhiteSpace(applicantEmail))
            throw new ArgumentException("Applicant email is required.", nameof(applicantEmail));
        if (string.IsNullOrWhiteSpace(applicantPhone))
            throw new ArgumentException("Applicant phone is required.", nameof(applicantPhone));

        Id = Guid.NewGuid();
        JobPostingId = jobPostingId;
        OrganizationId = organizationId;
        ApplicantName = applicantName.Trim();
        ApplicantEmail = applicantEmail.Trim().ToLowerInvariant();
        ApplicantPhone = applicantPhone.Trim();
        ApplicantUserId = string.IsNullOrWhiteSpace(applicantUserId) ? null : applicantUserId.Trim();
        ResumeReference = resumeReference?.Trim();
        CoverLetter = coverLetter?.Trim();
        Status = ApplicationStatus.Submitted;
        AppliedAtUtc = DateTime.UtcNow;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Moves the application to UnderReview status.
    /// </summary>
    public void Review(string reviewerUserId, DateTime utcNow, string? notes = null)
    {
        if (Status != ApplicationStatus.Submitted)
        {
            throw new InvalidOperationException($"Cannot move application to UnderReview from status {Status}.");
        }

        if (string.IsNullOrWhiteSpace(reviewerUserId))
            throw new ArgumentException("ReviewerUserId is required.", nameof(reviewerUserId));

        Status = ApplicationStatus.UnderReview;
        ReviewedByUserId = reviewerUserId.Trim();
        ReviewedAtUtc = utcNow;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            ReviewNotes = notes.Trim();
        }
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Shortlists the application for interview or advanced screening.
    /// </summary>
    public void Shortlist(string reviewerUserId, DateTime utcNow, string? notes = null)
    {
        if (Status is not (ApplicationStatus.Submitted or ApplicationStatus.UnderReview))
        {
            throw new InvalidOperationException($"Cannot shortlist application from status {Status}.");
        }

        if (string.IsNullOrWhiteSpace(reviewerUserId))
            throw new ArgumentException("ReviewerUserId is required.", nameof(reviewerUserId));

        Status = ApplicationStatus.Shortlisted;
        ReviewedByUserId = reviewerUserId.Trim();
        ReviewedAtUtc = utcNow;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            ReviewNotes = notes.Trim();
        }
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Rejects the application with a reason.
    /// </summary>
    public void Reject(string reviewerUserId, string rejectionReason, DateTime utcNow, string? notes = null)
    {
        if (Status is ApplicationStatus.Accepted or ApplicationStatus.Withdrawn or ApplicationStatus.Rejected)
        {
            throw new InvalidOperationException($"Cannot reject application in terminal status {Status}.");
        }

        if (string.IsNullOrWhiteSpace(reviewerUserId))
            throw new ArgumentException("ReviewerUserId is required.", nameof(reviewerUserId));
        if (string.IsNullOrWhiteSpace(rejectionReason))
            throw new ArgumentException("RejectionReason is required.", nameof(rejectionReason));

        Status = ApplicationStatus.Rejected;
        RejectionReason = rejectionReason.Trim();
        ReviewedByUserId = reviewerUserId.Trim();
        ReviewedAtUtc = utcNow;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            ReviewNotes = notes.Trim();
        }
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Accepts the candidate application (offer extended).
    /// </summary>
    public void Accept(string reviewerUserId, DateTime utcNow, string? notes = null)
    {
        if (Status is not (ApplicationStatus.Shortlisted or ApplicationStatus.UnderReview))
        {
            throw new InvalidOperationException($"Cannot accept application from status {Status}. Application must be Shortlisted or UnderReview.");
        }

        if (string.IsNullOrWhiteSpace(reviewerUserId))
            throw new ArgumentException("ReviewerUserId is required.", nameof(reviewerUserId));

        Status = ApplicationStatus.Accepted;
        ReviewedByUserId = reviewerUserId.Trim();
        ReviewedAtUtc = utcNow;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            ReviewNotes = notes.Trim();
        }
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Allows candidate to withdraw their active application.
    /// </summary>
    public void Withdraw(DateTime utcNow)
    {
        if (Status is ApplicationStatus.Accepted or ApplicationStatus.Rejected or ApplicationStatus.Withdrawn)
        {
            throw new InvalidOperationException($"Cannot withdraw application in status {Status}.");
        }

        Status = ApplicationStatus.Withdrawn;
        UpdatedAtUtc = utcNow;
    }
}
